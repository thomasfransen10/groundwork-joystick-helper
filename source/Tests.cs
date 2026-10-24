using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace GroundworkJoystickHelper
{
    internal static class Tests
    {
        static int passed;
        static void Assert(bool condition,string name)
        {if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);passed++;}
        static void Throws(Action action,string name)
        {bool thrown=false;try{action();}catch{thrown=true;}Assert(thrown,name);}
        static string Q(string value){return "\""+value.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";}
        [STAThread] static int Main(string[] args)
        {
            if(args.Length==2&&args[0]=="--child-env")
            {File.WriteAllText(args[1],Environment.GetEnvironmentVariable("SteamAppId")+"|"+Environment.GetEnvironmentVariable("SteamGameId"));return 0;}
            try
            {
                string testRoot=Path.GetFullPath(args[0]);Directory.CreateDirectory(testRoot);
                Vdf parsed=Vdf.Parse("// fixture\n\"root\" { \"path\" \"D:\\\\Steam Library\" \"value\" \"a\\\"b\" }");
                Assert(parsed.Node("root").Text("path")==@"D:\Steam Library","VDF escaped Windows path");
                Assert(parsed.Node("root").Text("value")=="a\"b","VDF embedded quote");
                Throws(delegate{Vdf.Parse("\"broken\" {");},"Reject malformed Steam configuration");
                string primary=Path.Combine(testRoot,"Steam"),secondary=Path.Combine(testRoot,"Other Library");
                Directory.CreateDirectory(Path.Combine(primary,"steamapps"));
                Directory.CreateDirectory(Path.Combine(secondary,"steamapps","common","Groundwork Simulator"));
                string fakeGame=Path.Combine(secondary,"steamapps","common","Groundwork Simulator","game.exe");
                File.WriteAllBytes(fakeGame,new byte[]{0});
                File.WriteAllText(Path.Combine(primary,"steamapps","libraryfolders.vdf"),
                    "\"libraryfolders\" {\"0\" {\"path\" "+Q(primary)+"} \"1\" {\"path\" "+Q(secondary)+"}}");
                string manifest=Path.Combine(secondary,"steamapps","appmanifest_5306540.acf");
                File.WriteAllText(manifest,"\"AppState\" {\"appid\" \"5306540\" \"installdir\" \"Groundwork Simulator\"}");
                Assert(SteamFinder.Find(new[]{primary}).Single()==fakeGame,"Discover game in another library with spaces");
                File.WriteAllText(manifest,"\"AppState\" {\"appid\" \"5306540\" \"installdir\" \"..\\..\\outside\"}");
                Assert(SteamFinder.Find(new[]{primary}).Count==0,"Reject install-directory traversal");
                string smallCopy=Path.Combine(testRoot,"copy.bin");byte[] small={1,2,3};
                Compatibility.WriteVerifiedCopy(small,smallCopy);
                Assert(File.ReadAllBytes(smallCopy).SequenceEqual(small),"Create and verify separate copy");
                Compatibility.WriteVerifiedCopy(small,smallCopy);
                Assert(File.ReadAllBytes(smallCopy).SequenceEqual(small),"Reuse identical copy safely");
                Throws(delegate{Compatibility.WriteVerifiedCopy(new byte[]{9},smallCopy);},"Refuse to overwrite an unrelated file");
                Assert(File.ReadAllBytes(smallCopy).SequenceEqual(small),"Collision leaves existing bytes intact");
                Throws(delegate{Compatibility.Patch(new byte[]{1,2,3});},"Reject unknown game version before any output");
                string original=SteamFinder.Find(SteamFinder.KnownRoots()).FirstOrDefault();
                if(original==null)throw new Exception("Installed game required for real-image validation.");
                byte[] image=File.ReadAllBytes(original);string before=Compatibility.Hash(image);
                byte[] patched=Compatibility.Patch(image);
                Assert(image.Length==patched.Length,"Patched image retains exact file size");
                List<int> changes=new List<int>();for(int i=0;i<image.Length;i++)if(image[i]!=patched[i])changes.Add(i);
                Assert(changes.SequenceEqual(new[]{Compatibility.PatchOffset,Compatibility.PatchOffset+1}),"Only the two audited classifier bytes change");
                Assert(patched[Compatibility.PatchOffset]==0xB3&&patched[Compatibility.PatchOffset+1]==1,"Classifier false fallback now returns true");
                Assert(Compatibility.Hash(image)==before&&Compatibility.Hash(File.ReadAllBytes(original))==before,"Original image and installed game remain intact");
                image[0]^=1;
                Throws(delegate{Compatibility.Patch(image);},"Reject modified or updated game build");
                string oldApp=Environment.GetEnvironmentVariable("SteamAppId"),oldGame=Environment.GetEnvironmentVariable("SteamGameId");
                string probe=Path.Combine(testRoot,"child-env.txt");
                ProcessStartInfo info=Compatibility.StartInfo(Environment.ProcessPath);
                info.Arguments="--child-env \""+probe+"\"";info.CreateNoWindow=true;
                using(Process p=Process.Start(info)){Assert(p.WaitForExit(10000)&&p.ExitCode==0,"Launch test child successfully");}
                Assert(File.ReadAllText(probe)=="5306540|5306540","Child receives Steam application identity");
                Assert(Environment.GetEnvironmentVariable("SteamAppId")==oldApp&&Environment.GetEnvironmentVariable("SteamGameId")==oldGame,"Parent and system environment stay unchanged");
                Wgi.Init();List<Controller> devices=Wgi.Enumerate();Stopwatch wait=Stopwatch.StartNew();
                while(devices.Count<2&&wait.ElapsedMilliseconds<5000)
                {foreach(Controller d in devices)d.Dispose();System.Windows.Forms.Application.DoEvents();System.Threading.Thread.Sleep(100);devices=Wgi.Enumerate();}
                Assert(devices.Count>=2,"Detect both actual connected controllers");
                Assert(devices.Any(d=>d.Vendor==0x044F&&d.Product==0x0422),"Identify actual SOL-R without a hardcoded recognition rule");
                Assert(devices.Any(d=>d.Vendor==0x044F&&d.Product==0xB68D),"Identify actual HOTAS without a hardcoded recognition rule");
                foreach(Controller d in devices)
                {
                    Assert(d.Read(),"Read native state: "+d.Name);
                    Assert(d.Axes.All(x=>x>=0&&x<=1),"Native axes stay in documented range: "+d.Name);d.Dispose();
                }
                Wgi.Shutdown();
                Console.WriteLine("COMPLETED: "+passed+" assertions. Other controller models have not been tested.");
                return 0;
            }
            catch(Exception e){Console.WriteLine(e.ToString());return 1;}
        }
    }
}

