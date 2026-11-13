import struct,pathlib,json
root=pathlib.Path(r'E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\StreamingAssets\aa\StandaloneWindows64')
out=pathlib.Path(r'E:\SteamLibrary\steamapps\common\Probably Stolen\Modding\ChemAssistPlus\Research')
def lz4(b):
 o=bytearray(); i=0
 while i<len(b):
  t=b[i];i+=1;n=t>>4
  if n==15:
   while True:
    x=b[i];i+=1;n+=x
    if x!=255:break
  o+=b[i:i+n];i+=n
  if i>=len(b):break
  d=int.from_bytes(b[i:i+2],'little');i+=2;n=(t&15)+4
  if (t&15)==15:
   while True:
    x=b[i];i+=1;n+=x
    if x!=255:break
  for _ in range(n):o.append(o[-d])
 return bytes(o)
def unpack(p):
 b=p.read_bytes();i=0
 def z():
  nonlocal i
  j=b.index(0,i);v=b[i:j];i=j+1;return v
 z();ver=struct.unpack_from('>I',b,i)[0];i+=4;z();z()
 size,cs,us,fl=struct.unpack_from('>QIII',b,i);i+=20
 if ver>=7:i=(i+15)&~15
 info=b[-cs:] if fl&128 else b[i:i+cs]
 if fl&63:info=lz4(info)
 if not fl&128:i+=cs
 if fl&512:i=(i+15)&~15
 j=16;n=struct.unpack_from('>I',info,j)[0];j+=4;data=bytearray()
 for _ in range(n):
  u,c,f=struct.unpack_from('>IIH',info,j);j+=10
  block=b[i:i+c];i+=c;data+=lz4(block) if f&63 else block
 count=struct.unpack_from('>I',info,j)[0];j+=4
 for x in range(count):
  off,sz,flag=struct.unpack_from('>QQI',info,j);j+=20;k=info.index(0,j);name=info[j:k].decode();j=k+1
  (out/(p.stem+'-'+str(x)+'.bin')).write_bytes(data[off:off+sz])
for name in ['localization-assets-shared_assets_all.bundle','localization-string-tables-chinese(simplified)(zh)_assets_all.bundle']:unpack(root/name)
for p in out.glob('localization*.bin'):
 b=p.read_bytes()
 for key in [b'item_dream_dust_name',b'item_pink_alt_injector_name',b'item_fanta_pill_name']:
  i=b.find(key)
  if i>=0:print(p.name,key,i,b[max(0,i-20):i+len(key)+12].hex())
shared=(out/'localization-assets-shared_assets_all-0.bin').read_bytes()
keys=['item_'+k+'_name' for k in ['dream_dust','caffeine_pill','nightmare_dust','fanta_pill','zyanide_pill','birth_control_pill','pink_alt_injector','nightmare_cap']]+['liquid_'+k for k in ['nomore','methylol','reducing_agent','lipobond_agent','stabilizing_agent','nightmare_extract']]
results={}
for key in keys:
 pos=shared.find(key.encode())
 if pos<0:continue
 ident=shared[pos-12:pos-4]
 for p in out.glob('localization-string*.bin'):
  data=p.read_bytes();start=0
  while True:
   i=data.find(ident,start)
   if i<0:break
   start=i+1
   n=int.from_bytes(data[i+8:i+12],'little')
   if 0<n<20000:
    try:
     val=data[i+12:i+12+n].decode('utf-8');results[key]=val;print(key,repr(val))
    except UnicodeDecodeError:pass
(out/'chem-chinese-names.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
for needle in [b'methyl',b'fanta',b'dream_cap_extract']:
 start=0
 while True:
  i=shared.find(needle,start)
  if i<0:break
  start=i+1
  a=shared.rfind(b'liquid_',max(0,i-25),i+1)
  if a<0:continue
  n=int.from_bytes(shared[a-4:a],'little');key=shared[a:a+n].decode();ident=shared[a-12:a-4]
  for p in out.glob('localization-string*.bin'):
   data=p.read_bytes();j=data.find(ident)
   if j>=0:
    n=int.from_bytes(data[j+8:j+12],'little')
    if 0<n<500:
     try:results[key]=data[j+12:j+12+n].decode()
     except:pass
(out/'chem-chinese-names.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
