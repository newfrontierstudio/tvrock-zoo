using System.Runtime.InteropServices;
namespace TsScheduler;

// A viewport with Chrome-style chrome. Content keeps its native wheel and
// keyboard handling; only the visual scroll bar is replaced.
internal sealed class ChromeScrollPanel:Panel
{
    readonly Viewport viewport=new(){Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.White};
    readonly ModernScrollBar bar=new(){Dock=DockStyle.Right,Width=12};bool syncing;
    internal ChromeScrollPanel(){base.Controls.Add(viewport);base.Controls.Add(bar);viewport.Changed=Sync;bar.ValueChanged+=(_,_)=>{if(!syncing)viewport.AutoScrollPosition=new Point(0,bar.Value);};SizeChanged+=(_,_)=>Sync();}
    public new ControlCollection Controls=>viewport.Controls;
    public new bool AutoScroll{get=>viewport.AutoScroll;set=>viewport.AutoScroll=value;}
    public new Point AutoScrollPosition{get=>viewport.AutoScrollPosition;set=>viewport.AutoScrollPosition=value;}
    void Sync(){if(syncing||IsDisposed)return;syncing=true;try{int total=viewport.DisplayRectangle.Height;bar.Maximum=Math.Max(0,total-1);bar.LargeChange=Math.Max(1,viewport.ClientSize.Height);bar.Value=Math.Max(0,-viewport.AutoScrollPosition.Y);bar.Visible=total>viewport.ClientSize.Height;}finally{syncing=false;}}
    sealed class Viewport:Panel
    {
        internal Action? Changed;bool hiding;
        [DllImport("user32.dll")]static extern bool ShowScrollBar(IntPtr window,int which,bool show);
        void UpdateChrome(){if(hiding||!IsHandleCreated)return;hiding=true;try{ShowScrollBar(Handle,3,false);Changed?.Invoke();}finally{hiding=false;}}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);UpdateChrome();}
        protected override void OnScroll(ScrollEventArgs e){base.OnScroll(e);UpdateChrome();}
        protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg is 5 or 15 or 0x85 or 0x115)UpdateChrome();}
    }
}
internal sealed class ChromeCheckedListBox:CheckedListBox
{
    internal bool CheckLocked{get;set;}internal Action? Scrolled;bool hiding;
    [DllImport("user32.dll")]static extern bool ShowScrollBar(IntPtr window,int which,bool show);
    internal ChromeCheckedListBox(){IntegralHeight=false;}
    protected override void OnItemCheck(ItemCheckEventArgs e){if(CheckLocked)e.NewValue=e.CurrentValue;base.OnItemCheck(e);}
    protected override void WndProc(ref Message m){base.WndProc(ref m);if(!hiding&&IsHandleCreated&&(m.Msg is 5 or 15 or 0x85 or 0x115 or 0x20a)){hiding=true;try{ShowScrollBar(Handle,3,false);Scrolled?.Invoke();}finally{hiding=false;}}}
    internal Control Frame(){var frame=new Panel{Dock=DockStyle.Fill};var bar=new ModernScrollBar{Dock=DockStyle.Right,Width=12};Dock=DockStyle.Fill;frame.Controls.Add(this);frame.Controls.Add(bar);bool syncing=false;void Sync(){if(syncing||IsDisposed)return;syncing=true;try{bar.Maximum=Math.Max(0,Items.Count-1);bar.LargeChange=Math.Max(1,ClientSize.Height/Math.Max(1,ItemHeight));bar.SmallChange=1;bar.Value=TopIndex;bar.Visible=Items.Count>bar.LargeChange;}finally{syncing=false;}}bar.ValueChanged+=(_,_)=>{if(!syncing&&Items.Count>0)TopIndex=Math.Clamp(bar.Value,0,Items.Count-1);};Scrolled=Sync;SizeChanged+=(_,_)=>Sync();Sync();return frame;}
}
internal sealed class ResizableSettingsPane:Panel
{
    internal event EventHandler? HeightCommitted;
    internal ResizableSettingsPane(Control content,int width,int height,string name){Width=width;Height=height;MinimumSize=new Size(180,80);AccessibleName=name;Margin=new Padding(0,0,0,10);var grip=new ResizeGrip(this){Dock=DockStyle.Bottom,Height=8};content.Dock=DockStyle.Fill;Controls.Add(content);Controls.Add(grip);}
    sealed class ResizeGrip:Control
    {
        readonly ResizableSettingsPane owner;int startY,startHeight;bool dragging;
        internal ResizeGrip(ResizableSettingsPane owner){this.owner=owner;Cursor=Cursors.SizeNS;BackColor=Color.White;SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);AccessibleName="下辺をドラッグして高さを変更";}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(Color.FromArgb(185,190,194));e.Graphics.DrawLine(pen,0,Height/2,Width,Height/2);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;dragging=true;startY=PointToScreen(e.Location).Y;startHeight=owner.Height;Capture=true;}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)owner.Height=Math.Clamp(startHeight+PointToScreen(e.Location).Y-startY,owner.MinimumSize.Height,1200);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging)return;dragging=false;Capture=false;owner.HeightCommitted?.Invoke(owner,EventArgs.Empty);}
    }
}
