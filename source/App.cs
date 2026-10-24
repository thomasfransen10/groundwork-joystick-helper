using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GroundworkJoystickHelper
{
    internal sealed class HelperForm:Form
    {
        private readonly TextBox gamePath=new TextBox();
        private readonly Label gameStatus=new Label(), count=new Label(), liveStatus=new Label();
        private readonly ListView devices=new ListView(), axes=new ListView();
        private readonly Label buttons=new Label();
        private readonly Button start=new Button(), browse=new Button(), refresh=new Button();
        private readonly ProgressBar progress=new ProgressBar();
        private readonly Timer timer=new Timer();
        private readonly List<Controller> controllers=new List<Controller>();
        private bool supported,busy,closing;
        private int ticks;
        private string verifiedPath;
        internal bool PreviewMode;
        protected override bool ShowWithoutActivation { get { return PreviewMode; } }
        private static readonly Color Ink=Color.FromArgb(33,45,52), Green=Color.FromArgb(29,99,72);
        internal HelperForm()
        {
            Text="Groundwork Joystick Helper 0.3";
            Font=new Font("Segoe UI",10);ForeColor=Ink;BackColor=Color.FromArgb(245,247,246);
            ClientSize=new Size(940,740);MinimumSize=MaximumSize=new Size(956,779);
            StartPosition=FormStartPosition.CenterScreen;MaximizeBox=false;
            Label title=LabelAt("Groundwork Joystick Helper",24,18,880,36);
            title.Font=new Font("Segoe UI Semibold",21);title.ForeColor=Green;
            LabelAt("Find your controllers, test their input, then start the compatible game copy.",26,61,880,28);

            GroupBox installation=new GroupBox{Text="1  Game installation",Location=new Point(24,100),Size=new Size(892,133)};
            Controls.Add(installation);
            gamePath.SetBounds(16,29,738,30);gamePath.ReadOnly=true;installation.Controls.Add(gamePath);
            browse.Text="Browse…";browse.SetBounds(765,27,110,34);installation.Controls.Add(browse);
            browse.Click+=delegate
            {
                using(OpenFileDialog dialog=new OpenFileDialog{Filter="Original game.exe|game.exe",Title="Choose Groundwork Simulator's original game.exe"})
                    if(dialog.ShowDialog(this)==DialogResult.OK)SetGame(dialog.FileName);
            };
            gameStatus.SetBounds(16,71,855,42);installation.Controls.Add(gameStatus);

            GroupBox connected=new GroupBox{Text="2  Connected controllers",Location=new Point(24,246),Size=new Size(892,203)};
            Controls.Add(connected);
            count.SetBounds(16,26,710,25);connected.Controls.Add(count);
            refresh.Text="Refresh";refresh.SetBounds(765,21,110,33);connected.Controls.Add(refresh);refresh.Click+=delegate{RefreshControllers(true);};
            devices.SetBounds(16,61,859,124);devices.View=View.Details;devices.FullRowSelect=true;devices.HideSelection=false;
            devices.MultiSelect=false;devices.GridLines=false;connected.Controls.Add(devices);
            foreach(object[] col in new[]{new object[]{"Controller",250},new object[]{"Windows name",215},new object[]{"Type",130},new object[]{"Axes",55},new object[]{"Buttons",65},new object[]{"Input",115}})
                devices.Columns.Add((string)col[0],(int)col[1]);
            devices.SelectedIndexChanged+=delegate{ResetAxisRows();};

            GroupBox testing=new GroupBox{Text="Live input — select a controller and move it",Location=new Point(24,461),Size=new Size(892,142)};
            Controls.Add(testing);
            axes.SetBounds(16,27,527,97);axes.View=View.Details;axes.FullRowSelect=true;testing.Controls.Add(axes);
            axes.Columns.Add("Axis",70);axes.Columns.Add("Position",100);axes.Columns.Add("Observed range",310);
            buttons.SetBounds(560,30,310,55);testing.Controls.Add(buttons);
            liveStatus.SetBounds(560,88,310,38);testing.Controls.Add(liveStatus);

            LabelAt("3  In the game: Controls → Joystick → Set up sticks. Assign left/right, then bind your controls.",26,615,884,30);
            LabelAt("Accepts Windows gaming-controller names in any language. Input and bindings remain native.",26,646,884,25).ForeColor=Color.DimGray;
            start.Text="Start game";start.Font=new Font("Segoe UI Semibold",11);start.BackColor=Green;start.ForeColor=Color.White;
            start.FlatStyle=FlatStyle.Flat;start.SetBounds(710,679,206,40);Controls.Add(start);start.Enabled=false;
            start.Click+=delegate{StartGame();};
            progress.SetBounds(27,688,650,15);progress.Visible=false;Controls.Add(progress);
            LabelAt("Windows 10/11 · 64-bit · supported game build: 0.1.1 / 26171350",26,688,674,24).Name="footer";
            Shown+=delegate
            {
                try
                {
                    List<string> games=SteamFinder.Find(SteamFinder.KnownRoots());
                    if(games.Count>0)SetGame(games[0]);
                    else{gameStatus.Text="Game not found automatically. Choose its original game.exe with Browse.";gameStatus.ForeColor=Color.DarkOrange;}
                    RefreshControllers(true);
                }
                catch(Exception e){gameStatus.Text=e.Message;}
                timer.Interval=100;timer.Tick+=delegate{TickInput();};timer.Start();
            };
            FormClosed+=delegate{closing=true;timer.Stop();foreach(Controller d in controllers)d.Dispose();Wgi.Shutdown();};
        }
        private Label LabelAt(string text,int x,int y,int w,int h)
        {Label label=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h)};Controls.Add(label);return label;}
        private void SetGame(string path)
        {
            if(busy)return;
            gamePath.Text=path;supported=false;start.Enabled=false;gameStatus.ForeColor=Ink;
            gameStatus.Text="Checking the game version…";busy=true;browse.Enabled=false;
            Task.Factory.StartNew(delegate{return Compatibility.Supported(path);}).ContinueWith(delegate(Task<bool> task)
            {
                if(closing||IsDisposed)return;
                BeginInvoke((Action)delegate
                {
                    busy=false;browse.Enabled=true;
                    supported=!task.IsFaulted&&task.Result;verifiedPath=supported?path:null;
                    gameStatus.ForeColor=supported?Green:Color.Firebrick;
                    gameStatus.Text=supported?"Ready — supported game version. The original game.exe is preserved.":
                        "Unsupported or unreadable game version. Supported build: "+Compatibility.Build+". No game file will be changed.";
                    UpdateStart();
                });
            });
        }
        private void UpdateStart(){start.Enabled=supported&&!busy&&controllers.Any(d=>d.AxisCount>=2);}
        private void RefreshControllers(bool force)
        {
            try
            {
                List<Controller> fresh=Wgi.Enumerate();
                if(!force&&fresh.Select(d=>d.Id).OrderBy(x=>x).SequenceEqual(controllers.Select(d=>d.Id).OrderBy(x=>x)))
                {foreach(Controller d in fresh)d.Dispose();return;}
                string selected=Selected()==null?null:Selected().Id;
                foreach(Controller d in controllers)d.Dispose();controllers.Clear();controllers.AddRange(fresh);
                devices.BeginUpdate();devices.Items.Clear();
                foreach(Controller d in controllers)
                {
                    ListViewItem row=new ListViewItem(new[]{d.Name,d.WindowsName,d.IsGamepad?"Gamepad":"Native controller",
                        d.AxisCount.ToString(),d.ButtonCount.ToString(),"Move to test"});
                    row.Tag=d;devices.Items.Add(row);if(d.Id==selected)row.Selected=true;
                }
                if(devices.SelectedItems.Count==0&&devices.Items.Count>0)devices.Items[0].Selected=true;
                devices.EndUpdate();ResetAxisRows();
                int candidates=controllers.Count(d=>d.AxisCount>=2&&!d.IsGamepad);
                count.ForeColor=controllers.Count>0?Green:Color.DarkOrange;
                count.Text=controllers.Count+" controller(s) available to the game · "+candidates+" joystick/wheel candidate(s)";
                if(controllers.Count==0)count.Text="No Windows gaming controllers found. Check USB connections and the manufacturer's driver.";
                UpdateStart();
            }
            catch(Exception e){count.Text="Controller detection unavailable: "+e.Message;count.ForeColor=Color.Firebrick;}
        }
        private Controller Selected(){return devices.SelectedItems.Count==0?null:devices.SelectedItems[0].Tag as Controller;}
        private void ResetAxisRows()
        {
            axes.Items.Clear();Controller d=Selected();if(d==null)return;
            for(int i=0;i<d.AxisCount;i++)axes.Items.Add(new ListViewItem(new[]{"Axis "+(i+1),"—","Move this axis through its range"}));
        }
        private void TickInput()
        {
            if(closing)return;
            foreach(ListViewItem row in devices.Items)
            {
                Controller d=(Controller)row.Tag;
                row.SubItems[5].Text=d.Read()?(d.Changes>0?"Receiving input":"Move to test"):"Unavailable";
            }
            Controller current=Selected();
            if(current!=null)
            {
                for(int i=0;i<Math.Min(current.AxisCount,axes.Items.Count);i++)
                {
                    axes.Items[i].SubItems[1].Text=(current.Axes[i]*100).ToString("0.0")+"%";
                    axes.Items[i].SubItems[2].Text=Double.IsInfinity(current.Minimum[i])?"—":
                        (current.Minimum[i]*100).ToString("0.0")+"% to "+(current.Maximum[i]*100).ToString("0.0")+"%";
                }
                List<string> pressed=new List<string>();
                for(int i=0;i<current.Buttons.Length;i++)if(current.Buttons[i]!=0)pressed.Add((i+1).ToString());
                buttons.Text="Pressed buttons: "+(pressed.Count==0?"none":String.Join(", ",pressed.ToArray()));
                liveStatus.Text=current.Error.Length>0?current.Error:current.Changes+" input update(s) seen";
            }
            // Refresh the device inventory every three seconds, preserving live
            // readings and selection when the attached device identities are equal.
            ticks++;if(ticks%30==0)RefreshControllers(false);
        }
        private void StartGame()
        {
            if(busy||!supported||verifiedPath!=gamePath.Text)return;
            if(Compatibility.IsRunning(verifiedPath))
            {MessageBox.Show(this,"Save your session and close Groundwork Simulator first. Then click Start game again.",Text,MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            if(!controllers.Any(d=>d.AxisCount>=2))
            {MessageBox.Show(this,"Connect a controller with at least two axes, then refresh.",Text);return;}
            busy=true;start.Enabled=false;browse.Enabled=false;refresh.Enabled=false;
            progress.Visible=true;progress.Style=ProgressBarStyle.Marquee;Controls.Find("footer",true)[0].Visible=false;
            string original=verifiedPath;
            gameStatus.Text="Preparing compatibility mode…";
            Task.Factory.StartNew(delegate
            {
                Compatibility.BackupSettings();
                return Compatibility.Prepare(original);
            }).ContinueWith(delegate(Task<string> task)
            {
                if(closing||IsDisposed)return;
                BeginInvoke((Action)delegate
                {
                    busy=false;progress.Visible=false;Controls.Find("footer",true)[0].Visible=true;browse.Enabled=true;refresh.Enabled=true;UpdateStart();
                    if(task.IsFaulted)
                    {
                        Exception error=task.Exception.GetBaseException();
                        gameStatus.Text="Could not start: "+error.Message;gameStatus.ForeColor=Color.Firebrick;
                        MessageBox.Show(this,error.Message+"\n\nIf the game folder is read-only, this helper needs write access to create its separate copy.",Text,MessageBoxButtons.OK,MessageBoxIcon.Information);
                        return;
                    }
                    try
                    {
                        using(Process p=Process.Start(Compatibility.StartInfo(task.Result)))Compatibility.Log("Started compatible game copy, pid="+p.Id);
                        gameStatus.Text="Game started. Assign left/right in Controls → Joystick → Set up sticks.";gameStatus.ForeColor=Green;
                        WindowState=FormWindowState.Minimized;
                    }
                    catch(Exception e){MessageBox.Show(this,e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}
                });
            });
        }
    }
    internal static class App
    {
        [STAThread] private static int Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                if(args.Length==2&&args[0]=="--diagnose")
                {
                    Wgi.Init();List<Controller> devices=Wgi.Enumerate();Stopwatch wait=Stopwatch.StartNew();
                    while(devices.Count==0&&wait.ElapsedMilliseconds<4000)
                    {Application.DoEvents();System.Threading.Thread.Sleep(100);devices=Wgi.Enumerate();}
                    List<string> lines=new List<string>{"Groundwork Joystick Helper "+Compatibility.Version,"Detected installations:"};
                    foreach(string path in SteamFinder.Find(SteamFinder.KnownRoots()))lines.Add(path+" | supported="+Compatibility.Supported(path));
                    lines.Add("Connected controllers (no device IDs or serial numbers exported):");
                    foreach(Controller d in devices){d.Read();lines.Add(d.Name+" | Windows="+d.WindowsName+" | axes="+d.AxisCount+" | buttons="+d.ButtonCount+" | gamepad="+d.IsGamepad+" | input="+(d.Error.Length==0?"readable":d.Error));d.Dispose();}
                    File.WriteAllLines(args[1],lines.ToArray());Wgi.Shutdown();return 0;
                }
                if(args.Length==2&&args[0]=="--render-preview")
                {
                    using(HelperForm form=new HelperForm())
                    {
                        form.PreviewMode=true;form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);
                        form.Show();Stopwatch wait=Stopwatch.StartNew();
                        while(wait.ElapsedMilliseconds<4500){Application.DoEvents();System.Threading.Thread.Sleep(50);}
                        using(Bitmap bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(args[1]);}
                        form.Close();
                    }
                    return 0;
                }
                Application.Run(new HelperForm());return 0;
            }
            catch(Exception e){MessageBox.Show(e.Message,"Groundwork Joystick Helper",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
        }
    }
}
