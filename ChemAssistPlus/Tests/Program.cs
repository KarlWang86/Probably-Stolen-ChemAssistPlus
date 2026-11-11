using System;
using ChemAssistPlus;

static class Program
{
    static void Expect(long actual, long expected, string label)
    { if (actual != expected) throw new Exception(label + ": expected " + expected + ", got " + actual); }
    static void Main()
    {
        Expect(DoseMath.RequiredSolution(364000, 700000, 1000000), 520000, "70% alcohol carries its 30% water");
        Expect(DoseMath.RequiredSolution(5300, 97000, 100000), 5464, "97% acid solution rounds to microlitres");
        Expect(DoseMath.RequiredSolution(0, 700, 1000), 0, "Already satisfied needs no dosing");
        Expect(DoseMath.RequiredSolution(1, 1, int.MaxValue), int.MaxValue, "Trace source is not pure");
        Expect(DoseMath.RequiredSolution(int.MaxValue, 1, int.MaxValue), 4611686014132420609L, "Large request does not overflow");
        var random = new Random(3127);
        for (int n = 0; n < 20000; n++)
        {
            int total = random.Next(1, 2000000), active = random.Next(1, total + 1), desired = random.Next(0, 2000000);
            long solution = DoseMath.RequiredSolution(desired, active, total);
            if (solution * active < (long)desired * total) throw new Exception("Underdosed active ingredient");
            if (solution > 0 && (solution - 1) * active >= (long)desired * total) throw new Exception("Unnecessary extra solution");
        }
        bool rejected = false;
        try { DoseMath.RequiredSolution(1, 0, 1000); } catch (ArgumentOutOfRangeException) { rejected = true; }
        if (!rejected) throw new Exception("Zero-concentration source accepted");
        Console.WriteLine("PASS: dilution, microlitre rounding, trace source, overflow, 20,000 dosing boundary cases, invalid concentration.");
    }
}

