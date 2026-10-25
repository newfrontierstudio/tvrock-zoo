using System.Runtime.InteropServices;
namespace TsScheduler;

// Retain WinForms' vertical layout/scroll range, but render only a slim custom thumb.
internal sealed class ChromeMediaList:FlowLayoutPanel
{
    readonly ThumbBar bar;bool hiding;
    [DllImport("user32.dll")]static extern bool ShowScrollBar(IntPtr window,int which,bool show);
    internal ChromeMediaList(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);bar=new ThumbBar(this);}
    internal Control CreateFrame(){var host=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Margin=Padding.Empty};bar.Dock=DockStyle.Right;bar.Width=12;host.Controls.Add(this);host.Controls.Add(bar);return host;}
    internal int Offset=>Math.Max(0,-AutoScrollPosition.Y);
    internal int MaximumOffset=>Math.Max(0,DisplayRectangle.Height-ClientSize.Height);
    internal void SetOffset(int offset,bool notify=true){int old=Offset;AutoScrollPosition=new Point(0,Math.Clamp(offset,0,MaximumOffset));HideNative();bar.Invalidate();if(notify&&Offset!=old)OnScroll(new ScrollEventArgs(ScrollEventType.ThumbPosition,old,Offset,ScrollOrientation.VerticalScroll));}
    internal void Wheel(int delta){int lines=SystemInformation.MouseWheelScrollLines;SetOffset(Offset-(int)Math.Round(delta/120d*(lines<0?ClientSize.Height:Math.Max(1,lines)*Math.Max(20,Font.Height))));}
    protected override void OnMouseWheel(MouseEventArgs e)=>Wheel(e.Delta);
    protected override Point ScrollToControl(Control activeControl)=>DisplayRectangle.Location;
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.Style&=~(0x00100000|0x00200000);return p;}}
    void HideNative(){if(hiding||!IsHandleCreated)return;hiding=true;try{ShowScrollBar(Handle,3,false);}finally{hiding=false;}}
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);HideNative();bar?.Invalidate();}
    protected override void OnScroll(ScrollEventArgs e){base.OnScroll(e);HideNative();bar.Invalidate();}
    protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg is 0x0005 or 0x000f or 0x0085 or 0x0114 or 0x0115)HideNative();}
    sealed class ThumbBar:Control
    {
        readonly ChromeMediaList owner;bool hover,dragging;int grab;
        internal ThumbBar(ChromeMediaList owner){this.owner=owner;DoubleBuffered=true;TabStop=false;BackColor=Color.White;Cursor=Cursors.Hand;}
        Rectangle Thumb{get{if(Height<=0||owner.MaximumOffset==0)return Rectangle.Empty;float scale=DeviceDpi/96f;int height=Math.Clamp((int)(Height*(double)owner.ClientSize.Height/Math.Max(1,owner.DisplayRectangle.Height)),Math.Min(Height,(int)(30*scale)),Height);int width=Math.Min(Width,Math.Max(3,(int)((hover?6:4)*scale)));int y=(int)((Height-height)*(double)owner.Offset/owner.MaximumOffset);return new Rectangle((Width-width)/2,y,width,height);}}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var rect=Thumb;if(rect.IsEmpty)return;using var fill=new SolidBrush(hover?Color.FromArgb(139,139,139):Color.FromArgb(184,184,184));using var shape=RoundedControls.Shape(rect,Math.Max(1,rect.Width/2f));e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.FillPath(fill,shape);}
        void MoveThumb(int y){var thumb=Thumb;if(thumb.IsEmpty)return;owner.SetOffset((int)Math.Round((y-grab)*owner.MaximumOffset/(double)Math.Max(1,Height-thumb.Height)));}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||Thumb.IsEmpty)return;var thumb=Thumb;grab=thumb.Contains(e.Location)?e.Y-thumb.Top:thumb.Height/2;dragging=true;Capture=true;MoveThumb(e.Y);}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)MoveThumb(e.Y);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;}
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
        protected override void OnMouseWheel(MouseEventArgs e)=>owner.Wheel(e.Delta);
    }
}
