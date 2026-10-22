using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace ZooRecording;

internal sealed record RecordingActivity(string Station,string Title,DateTimeOffset End)
{
    internal string Tuner{get;init;}="";
}
internal sealed class RecordingIndicator:IDisposable
{
    readonly Form owner;readonly NotifyIcon tray;readonly Icon idle;readonly string app;
    readonly Dictionary<int,Icon> icons=[];readonly System.Windows.Forms.Timer hoverTimer=new(){Interval=100};
    readonly HoverWindow hover=new();readonly TaskbarMessages messages;
    ITaskbarList3? taskbar;string fullText="",signature="";Point anchor;DateTime hoverSince;bool hovering,disposed;
    internal int Count{get;private set;}internal string Details=>fullText;
    internal RecordingIndicator(Form owner,NotifyIcon tray,Icon idle,string app)
    {
        this.owner=owner;this.tray=tray;this.idle=idle;this.app=app;messages=new TaskbarMessages(()=>ApplyTaskbar());
        owner.HandleCreated+=(_,_)=>{messages.Attach(owner.Handle);};owner.HandleDestroyed+=(_,_)=>messages.Detach();
        if(owner.IsHandleCreated)messages.Attach(owner.Handle);owner.Shown+=(_,_)=>ApplyTaskbar();
        tray.Text=app+" — 録画なし";
    }
    internal static string BadgeText(int count)=>count>=100?"99+":Math.Max(0,count).ToString(System.Globalization.CultureInfo.InvariantCulture);
    internal static Icon CreateCountIcon(int count,bool studio=false)
    {
        using var bitmap=new Bitmap(64,64);using var g=Graphics.FromImage(bitmap);g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var path=new GraphicsPath();path.AddArc(2,2,16,16,180,90);path.AddArc(46,2,16,16,270,90);path.AddArc(46,46,16,16,0,90);path.AddArc(2,46,16,16,90,90);path.CloseFigure();
        using var red=new SolidBrush(studio?Color.FromArgb(239,197,203):Color.FromArgb(204,35,51));g.FillPath(red,path);
        string text=BadgeText(count);using var font=new Font("Segoe UI",text.Length==1?49:text.Length==2?39:28,FontStyle.Bold,GraphicsUnit.Pixel);using var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};using var number=new SolidBrush(studio?Color.FromArgb(139,70,80):Color.White);g.DrawString(text,font,number,new RectangleF(0,-2,64,64),format);
        IntPtr handle=bitmap.GetHicon();try{return (Icon)Icon.FromHandle(handle).Clone();}finally{DestroyIcon(handle);}
    }
    internal static string Describe(string app,IReadOnlyList<RecordingActivity> rows,string note="")=>rows.Count==0?app+" — 録画なし"+note:$"{app} — {rows.Count}局録画中{note}\n"+string.Join("\n",rows.Select(r=>$"{(r.Tuner.Length>0?Clean(r.Tuner)+"／":"")}{Clean(r.Station)}：{Clean(r.Title)}（{r.End.ToLocalTime():HH:mm}まで）"));
    internal static string NativeTip(string app,IReadOnlyList<RecordingActivity> rows)
    {
        string text=rows.Count==0?app+" — 録画なし":string.Join("\n",rows.Select(r=>$"{Clean(r.Station)}：{Clean(r.Title)}"));
        if(text.Length<=63)return text;
        int length=62;if(char.IsHighSurrogate(text[length-1]))length--;
        return text[..length]+"…";
    }
    static string Clean(string text)=>text.Replace('\r',' ').Replace('\n',' ').Replace('\0',' ').Trim();
    internal void Update(IEnumerable<RecordingActivity> recordings,string note="")
    {
        if(disposed)return;var rows=recordings.OrderBy(r=>r.End).ThenBy(r=>r.Station).ToArray();string next=Describe(app,rows,note);if(next==signature)return;signature=fullText=next;Count=rows.Length;
        Icon icon=idle;if(Count>0){int key=Math.Min(Count,100);if(!icons.TryGetValue(key,out icon!))icons[key]=icon=CreateCountIcon(key,app=="ZOOstudio Pre");}
        owner.Icon=icon;tray.Icon=icon;tray.Text=NativeTip(app,rows);
        if(hover.Visible)ShowHover();ApplyTaskbar();
    }
    void ApplyTaskbar()
    {
        if(disposed||!owner.IsHandleCreated)return;
        try{taskbar??=(ITaskbarList3)new TaskbarList();taskbar.HrInit();taskbar.SetThumbnailTooltip(owner.Handle,fullText.Length>1000?fullText[..997]+"…":fullText);}catch(COMException){}catch(InvalidCastException){}
    }
    void TrayMove(object? sender,MouseEventArgs e){if(!hovering||Math.Abs(Cursor.Position.X-anchor.X)>8||Math.Abs(Cursor.Position.Y-anchor.Y)>8){anchor=Cursor.Position;hoverSince=DateTime.Now;hovering=true;}}
    void TrayDown(object? sender,MouseEventArgs e){hover.Hide();hovering=false;}
    void HoverTick(object? sender,EventArgs e){if(!hovering)return;var point=Cursor.Position;if(!new Rectangle(anchor.X-20,anchor.Y-14,40,28).Contains(point)&&!hover.Bounds.Contains(point)){hover.Hide();hovering=false;return;}if(!hover.Visible&&(DateTime.Now-hoverSince).TotalMilliseconds>=450)ShowHover();}
    void ShowHover(){string[] lines=fullText.Split('\n');string text=string.Join("\n",lines.Take(13));if(lines.Length>13)text+=$"\nほか {lines.Length-13}件";hover.Display(text,anchor);}
    public void Dispose(){if(disposed)return;disposed=true;hoverTimer.Stop();hoverTimer.Dispose();tray.MouseMove-=TrayMove;tray.MouseDown-=TrayDown;hover.Dispose();messages.Detach();foreach(var icon in icons.Values)icon.Dispose();if(taskbar!=null)Marshal.FinalReleaseComObject(taskbar);}
    [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr handle);
    sealed class HoverWindow:Form
    {
        readonly Label text=new(){Dock=DockStyle.Fill,UseMnemonic=false,Padding=new Padding(12),BackColor=Color.FromArgb(255,255,247)};
        internal HoverWindow(){FormBorderStyle=FormBorderStyle.None;Padding=new Padding(1);BackColor=Color.FromArgb(175,181,190);ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Font=new Font("Meiryo UI",10);Controls.Add(text);}
        protected override bool ShowWithoutActivation=>true;
        protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
        internal void Display(string value,Point point){var area=Screen.FromPoint(point).WorkingArea;int width=Math.Min(560,area.Width-30);var size=TextRenderer.MeasureText(value,Font,new Size(width-24,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);text.Text=value;ClientSize=new Size(width,Math.Min(area.Height-30,size.Height+26));Location=new Point(Math.Clamp(point.X+12,area.Left,Math.Max(area.Left,area.Right-Width)),Math.Clamp(point.Y-Height-12,area.Top,Math.Max(area.Top,area.Bottom-Height)));if(!Visible)Show();}
    }
    sealed class TaskbarMessages(Action changed):NativeWindow
    {
        static readonly int Created=RegisterWindowMessage("TaskbarButtonCreated");
        internal void Attach(IntPtr handle){Detach();AssignHandle(handle);}internal void Detach(){if(Handle!=IntPtr.Zero)ReleaseHandle();}
        protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg==Created)changed();}
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int RegisterWindowMessage(string value);
    }
    [ComImport,Guid("56FDF344-FD6D-11D0-958A-006097C9A090")]class TaskbarList{}
    // Vtable order follows the Windows SDK's ITaskbarList3 declaration.
    [ComImport,Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ITaskbarList3
    {
        void HrInit();void AddTab(IntPtr hwnd);void DeleteTab(IntPtr hwnd);void ActivateTab(IntPtr hwnd);void SetActiveAlt(IntPtr hwnd);void MarkFullscreenWindow(IntPtr hwnd,[MarshalAs(UnmanagedType.Bool)]bool fullscreen);
        void SetProgressValue(IntPtr hwnd,ulong completed,ulong total);void SetProgressState(IntPtr hwnd,uint flags);void RegisterTab(IntPtr tab,IntPtr mdi);void UnregisterTab(IntPtr tab);void SetTabOrder(IntPtr tab,IntPtr before);void SetTabActive(IntPtr tab,IntPtr mdi,uint reserved);
        void ThumbBarAddButtons(IntPtr hwnd,uint count,IntPtr buttons);void ThumbBarUpdateButtons(IntPtr hwnd,uint count,IntPtr buttons);void ThumbBarSetImageList(IntPtr hwnd,IntPtr images);void SetOverlayIcon(IntPtr hwnd,IntPtr icon,[MarshalAs(UnmanagedType.LPWStr)]string description);void SetThumbnailTooltip(IntPtr hwnd,[MarshalAs(UnmanagedType.LPWStr)]string tip);
    }
}
