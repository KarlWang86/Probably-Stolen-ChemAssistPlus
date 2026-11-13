using Steamworks;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Explicit invocation only. Never creates an item or starts the game.
const uint App = 4348910;
const ulong Item = 3836818025;
if (args.Length < 2 || args[1] is not ("--check" or "--diagnose" or "--publish" or "--schinese-only"))
{
    Console.Error.WriteLine("WorkshopPublisher <WorkshopUpload directory> --check|--diagnose|--publish|--schinese-only [additional image paths...]");
    return 2;
}
var workspace = Path.GetFullPath(args[0]);
var previews = args.Skip(2).Select(Path.GetFullPath).ToArray();
if (previews.Length > 0 && args[1] != "--publish")
    throw new Exception("Additional previews require a full publish; do not add them again on a Chinese-only retry.");
var previewHashes = new Dictionary<string, string>();
foreach (var path in previews)
{
    var info = new FileInfo(path);
    if (!info.Exists || info.Length == 0 || info.Length >= 1_000_000
        || !new[] { ".png", ".jpg", ".jpeg", ".gif" }.Contains(info.Extension.ToLowerInvariant()))
        throw new Exception("Additional preview must be an existing PNG/JPG/GIF under 1 MB: " + path);
    previewHashes.Add(path, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
}
var fields = Regex.Matches(File.ReadAllText(Path.Combine(workspace, "upload.vdf")), "\"([^\"\\r\\n]+)\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"")
    .ToDictionary(m => m.Groups[1].Value, m => Regex.Replace(m.Groups[2].Value, @"\\([""\\nrt])", e => e.Groups[1].Value switch { "n" => "\n", "r" => "\r", "t" => "\t", var s => s }));
if (fields["appid"] != App.ToString() || fields["publishedfileid"] != Item.ToString() || fields["visibility"] != "0")
    throw new Exception("Unexpected AppID, item ID or visibility.");
var description = fields["description"];
if (Encoding.UTF8.GetByteCount(description) >= 8000 || string.IsNullOrWhiteSpace(fields["title"]))
    throw new Exception("Invalid title/description.");
foreach (var pair in new[] { ("description", "创意工坊简介.txt"), ("changenote", "本次更新说明.txt") })
    if (fields[pair.Item1] != File.ReadAllText(Path.Combine(workspace, pair.Item2)).TrimEnd('\r', '\n'))
        throw new Exception("VDF text differs from prepared source: " + pair.Item1);
var hashes = new Dictionary<string, string>();
foreach (var line in File.ReadAllLines(Path.Combine(workspace, "content-sha256.txt")))
{
    var parts = line.Split("  ", 2);
    if (parts.Length != 2 || !new[] { "ChemAssistPlus.dll", "manifest.xml", "使用说明.txt" }.Contains(parts[1]))
        throw new Exception("Unexpected content hash entry.");
    var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(fields["contentfolder"], parts[1]))));
    if (!actual.Equals(parts[0], StringComparison.OrdinalIgnoreCase)) throw new Exception("Content hash mismatch: " + parts[1]);
    hashes.Add(parts[1], actual);
}
if (hashes.Count != 3) throw new Exception("Expected exactly three runtime files.");
var manifest = System.Xml.Linq.XDocument.Load(Path.Combine(fields["contentfolder"], "manifest.xml"));
var version = manifest.Descendants("ModVersion").Single().Value;
var receiptPath = Path.Combine(workspace, $"upload-result-{version}-languages.json");
var stages = new List<object>();
void Save(string status) => File.WriteAllText(receiptPath, JsonSerializer.Serialize(new {
    version, appid = App.ToString(), publishedfileid = Item.ToString(), status,
    updatedAt = DateTimeOffset.Now, files = hashes, additionalPreviews = previewHashes,
    descriptionUtf8Bytes = Encoding.UTF8.GetByteCount(description), stages
}, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(true));
Environment.SetEnvironmentVariable("SteamAppId", App.ToString());
Environment.SetEnvironmentVariable("SteamGameId", App.ToString());
bool initialized = false;
try
{
    if (!(initialized = SteamAPI.Init())) throw new Exception("SteamAPI.Init failed. Steam must be running and signed in to the workshop owner account.");
    if (!SteamUser.BLoggedOn()) throw new Exception("Steam client is offline; reconnect before publishing.");
    Console.WriteLine($"Steam initialized; AppID={SteamUtils.GetAppID()}, owner={SteamUser.GetSteamID()}");
    if (args[1] == "--diagnose")
        Console.WriteLine($"License: subscribed={SteamApps.BIsSubscribedApp(new AppId_t(App))}, appOwner={SteamApps.GetAppOwner()}, familySharing={SteamApps.BIsSubscribedFromFamilySharing()}, VACBanned={SteamApps.BIsVACBanned()}");
    if (SteamUtils.GetAppID().m_AppId != App) throw new Exception("Steam AppID mismatch.");
    T Wait<T>(SteamAPICall_t call) where T : struct
    {
        bool done = false, failed = false;
        T result = default;
        using var callback = CallResult<T>.Create((r, io) => { result = r; failed = io; done = true; });
        callback.Set(call);
        var timer = Stopwatch.StartNew();
        while (!done && timer.Elapsed < TimeSpan.FromMinutes(4)) { SteamAPI.RunCallbacks(); Thread.Sleep(100); }
        if (!done || failed) throw new Exception($"{typeof(T).Name}: timeout or IO failure; inspect receipt and remote state before retrying.");
        return result;
    }
    SteamUGCDetails_t Read(string language, bool verifyOwner)
    {
        var query = SteamUGC.CreateQueryUGCDetailsRequest(new[] { new PublishedFileId_t(Item) }, 1);
        try
        {
            if (!SteamUGC.SetLanguage(query, language) || !SteamUGC.SetReturnLongDescription(query, true) || !SteamUGC.SetAllowCachedResponse(query, 0))
                throw new Exception("Query configuration failed.");
            var result = Wait<SteamUGCQueryCompleted_t>(SteamUGC.SendQueryUGCRequest(query));
            if (result.m_eResult != EResult.k_EResultOK || result.m_unNumResultsReturned != 1 || !SteamUGC.GetQueryUGCResult(query, 0, out var details) || details.m_eResult != EResult.k_EResultOK)
                throw new Exception("Workshop query failed: " + result.m_eResult);
            if (details.m_nConsumerAppID.m_AppId != App || details.m_nPublishedFileId.m_PublishedFileId != Item || details.m_eVisibility != ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic)
                throw new Exception("Remote item identity/visibility mismatch.");
            if (verifyOwner && details.m_ulSteamIDOwner != SteamUser.GetSteamID().m_SteamID) throw new Exception("Signed-in user does not own this item.");
            return details;
        }
        finally { SteamUGC.ReleaseQueryUGCRequest(query); }
    }
    // Publishing needs only one ownership/identity check. Reading both languages is opt-in.
    foreach (var language in args[1] == "--check" ? new[] { "english", "schinese" } : new[] { "english" })
    {
        var before = Read(language, true);
        if (args[1] == "--diagnose")
            Console.WriteLine($"Item: creatorApp={before.m_nCreatorAppID}, consumerApp={before.m_nConsumerAppID}, banned={before.m_bBanned}; changenoteUtf8Bytes={Encoding.UTF8.GetByteCount(fields["changenote"])}");
        if (args[1] == "--schinese-only" && language == "english" && (before.m_rgchTitle != fields["title"] || before.m_rgchDescription != description))
            throw new Exception("Default-language text differs from this prepared release. Verify the content release before retrying Chinese alone.");
        Console.WriteLine($"{language}: current title={before.m_rgchTitle}; matches prepared={before.m_rgchDescription == description}");
    }
    if (args[1] is "--check" or "--diagnose") { Console.WriteLine("Read-only preflight passed; nothing submitted."); return 0; }
    Save("in-progress");
    foreach (var language in args[1] == "--schinese-only" ? new[] { "schinese" } : new[] { "english", "schinese" })
    {
        var handle = SteamUGC.StartItemUpdate(new AppId_t(App), new PublishedFileId_t(Item));
        if (!SteamUGC.SetItemUpdateLanguage(handle, language) || !SteamUGC.SetItemTitle(handle, fields["title"]) || !SteamUGC.SetItemDescription(handle, description))
            throw new Exception("SetItem language/title/description failed: " + language);
        // Upload runtime files/cover only once. Chinese pass only changes localized text.
        if (language == "english" && (!SteamUGC.SetItemContent(handle, fields["contentfolder"]) || !SteamUGC.SetItemPreview(handle, fields["previewfile"])))
            throw new Exception("SetItemContent/Preview failed.");
        if (language == "english")
            foreach (var path in previews)
            {
                if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) != previewHashes[path])
                    throw new Exception("Additional preview changed during publication: " + path);
                if (!SteamUGC.AddItemPreviewFile(handle, path, EItemPreviewType.k_EItemPreviewType_Image))
                    throw new Exception("AddItemPreviewFile failed: " + path);
                Console.WriteLine("Attached screenshot: " + Path.GetFileName(path));
            }
        Console.WriteLine($"Submitting {language}, content={language == "english"} ...");
        var result = Wait<SubmitItemUpdateResult_t>(SteamUGC.SubmitItemUpdate(handle, language == "english" ? fields["changenote"] : null));
        stages.Add(new { language, phase = "submit", result = result.m_eResult.ToString(), legalAgreementRequired = result.m_bUserNeedsToAcceptWorkshopLegalAgreement });
        Save("in-progress");
        if (result.m_eResult != EResult.k_EResultOK || result.m_bUserNeedsToAcceptWorkshopLegalAgreement)
            throw new Exception("Submission failed or legal agreement required: " + language + " / " + result.m_eResult);
        Console.WriteLine(language + ": Steam submission succeeded.");
    }
    Save("submitted");
    Console.WriteLine("Receipt: " + receiptPath);
    return 0;
}
catch (Exception e) { Console.Error.WriteLine(e.Message); if (args[1] is not ("--check" or "--diagnose")) Save("incomplete"); return 1; }
finally { if (initialized) SteamAPI.Shutdown(); }
