using System.Runtime.InteropServices;
namespace CMCutLab;
internal sealed class FileListBox:ListBox
{
 const int ScrollStyles=0x00300000;int wheelRemainder;bool hiding;readonly ThinScroll bar;
 readonly Dictionary<string,Bitmap> images=new(StringComparer.OrdinalIgnoreCase);readonly HashSet<string> failed=new(StringComparer.OrdinalIgnoreCase);readonly HashSet<string> pending=new(StringComparer.OrdinalIgnoreCase);readonly SemaphoreSlim thumbnailSlots=new(1,1);CancellationTokenSource thumbnails=new();bool showThumbnails;
 internal Func<FileInfo,CancellationToken,Task<Bitmap>>? ThumbnailFactory;
 internal bool ShowThumbnails{get=>showThumbnails;set{showThumbnails=value;ItemHeight=value?156:Math.Max(20,Font.Height+3);Invalidate();RefreshBar();}}
 internal void ResetThumbnails(){thumbnails.Cancel();thumbnails.Dispose();thumbnails=new();pending.Clear();failed.Clear();foreach(var image in images.Values)image.Dispose();images.Clear();}
 protected override void Dispose(bool disposing){if(disposing){thumbnails.Cancel();foreach(var image in images.Values)image.Dispose();thumbnails.Dispose();}base.Dispose(disposing);}
 async void LoadThumbnail(FileInfo file){if(ThumbnailFactory==null||failed.Contains(file.FullName)||pending.Count>=16||!pending.Add(file.FullName))return;var ct=thumbnails.Token;try{await thumbnailSlots.WaitAsync(ct);try{var image=await ThumbnailFactory(file,ct);if(ct.IsCancellationRequested||IsDisposed){image.Dispose();return;}if(images.Count>=48){string key=images.Keys.First();images[key].Dispose();images.Remove(key);}images[file.FullName]=image;}finally{thumbnailSlots.Release();}}catch(OperationCanceledException){}catch(Exception){if(!ct.IsCancellationRequested)failed.Add(file.FullName);}finally{if(!ct.IsCancellationRequested)pending.Remove(file.FullName);if(!IsDisposed&&!ct.IsCancellationRequested)Invalidate();}}
 protected override void OnDrawItem(DrawItemEventArgs e){if(e.Index<0||e.Index>=Items.Count)return;bool selected=(e.State&DrawItemState.Selected)!=0;using(var background=new SolidBrush(selected?Color.FromArgb(60,82,102):BackColor))e.Graphics.FillRectangle(background,e.Bounds);if(Items[e.Index] is not FileInfo file)return;var text=e.Bounds;text.Width-=12;if(!showThumbnails){TextRenderer.DrawText(e.Graphics,file.Name,Font,text,ForeColor,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);return;}var rect=new Rectangle(e.Bounds.Left+8,e.Bounds.Top+4,Math.Max(1,e.Bounds.Width-25),98);if(images.TryGetValue(file.FullName,out var image)){double scale=Math.Min((double)rect.Width/image.Width,(double)rect.Height/image.Height);int w=(int)(image.Width*scale),h=(int)(image.Height*scale);e.Graphics.DrawImage(image,rect.Left+(rect.Width-w)/2,rect.Top+(rect.Height-h)/2,w,h);}else{using var pen=new Pen(Color.FromArgb(72,78,85));e.Graphics.DrawRectangle(pen,rect);TextRenderer.DrawText(e.Graphics,"プレビュー",Font,rect,Color.Gray,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(!failed.Contains(file.FullName))LoadThumbnail(file);}text=new Rectangle(e.Bounds.Left+6,e.Bounds.Top+106,Math.Max(1,e.Bounds.Width-20),46);TextRenderer.DrawText(e.Graphics,file.Name,Font,text,ForeColor,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);}
 internal FileListBox(){DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=22;bar=new(this){Dock=DockStyle.Right,Width=9};Controls.Add(bar);}
 [DllImport("user32.dll")]static extern bool ShowScrollBar(IntPtr window,int which,bool show);
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowStyle(IntPtr window,int index);
 protected override CreateParams CreateParams{get{var cp=base.CreateParams;cp.Style&=~ScrollStyles;return cp;}}
 void RefreshBar(){if(!IsHandleCreated||hiding)return;if((GetWindowStyle(Handle,-16)&ScrollStyles)!=0){hiding=true;try{ShowScrollBar(Handle,3,false);}finally{hiding=false;}}bar?.Invalidate();}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);RefreshBar();}
 protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg is 0x0005 or 0x000f or 0x0100 or 0x0180 or 0x0184 or 0x0197)RefreshBar();}
 protected override void OnMouseWheel(MouseEventArgs e){wheelRemainder+=e.Delta;int steps=wheelRemainder/120;wheelRemainder%=120;if(Items.Count>0&&steps!=0){int rows=SystemInformation.MouseWheelScrollLines;rows=rows<0?Math.Max(1,ClientSize.Height/Math.Max(1,ItemHeight)):Math.Max(1,rows);TopIndex=Math.Clamp(TopIndex-steps*rows,0,Items.Count-1);}RefreshBar();}
 sealed class ThinScroll:Control
 {
  readonly FileListBox owner;bool hover,dragging;int offset;
  internal ThinScroll(FileListBox owner){this.owner=owner;SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.ResizeRedraw,true);TabStop=false;Cursor=Cursors.Hand;}
  int Rows=>Math.Max(1,owner.ClientSize.Height/Math.Max(1,owner.ItemHeight));int Maximum=>Math.Max(0,owner.Items.Count-Rows);
  Rectangle Thumb{get{int height=Math.Min(Height,Math.Max(28,Height*Rows/Math.Max(1,owner.Items.Count)));int y=Maximum==0?0:(int)((Height-height)*(double)Math.Min(owner.TopIndex,Maximum)/Maximum);return new Rectangle(hover?2:3,y,hover?5:3,height);}}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(owner.BackColor);if(Maximum==0)return;using var brush=new SolidBrush(hover?Color.FromArgb(124,133,142):Color.FromArgb(76,83,89));e.Graphics.FillRectangle(brush,Thumb);}
  void MoveThumb(int y){if(Maximum==0)return;owner.TopIndex=Math.Clamp((int)Math.Round((y-offset)*Maximum/(double)Math.Max(1,Height-Thumb.Height)),0,Maximum);Invalidate();}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||Maximum==0)return;var thumb=Thumb;offset=thumb.Contains(e.Location)?e.Y-thumb.Top:thumb.Height/2;dragging=true;Capture=true;MoveThumb(e.Y);}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)MoveThumb(e.Y);}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Left){dragging=false;Capture=false;}}
  protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
  protected override void OnMouseWheel(MouseEventArgs e)=>owner.OnMouseWheel(e);
 }
}
