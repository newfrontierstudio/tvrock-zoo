using System.Drawing.Drawing2D;
namespace TsScheduler;
internal sealed partial class MainForm
{
    readonly ToolTip _largeTip = new() { OwnerDraw=true,UseAnimation=false,UseFading=false,AutoPopDelay=25000,ShowAlways=true };
    readonly System.Windows.Forms.Timer _hoverDelay = new() { Interval=450 };
    readonly Font _hoverFont = new("Meiryo UI",12,FontStyle.Regular);
    Control? _hoverControl;
    string _hoverText="";
    Point _hoverPoint;
    float _readingSize=10;
    readonly List<Font> _readingFonts=[];
    static readonly Color Ink=Color.FromArgb(23,42,62);
    static readonly Color Accent=Color.FromArgb(29,91,160);
    void InitializeReadableDesign(FlowLayoutPanel toolbar)
    {
        var baseFont=new Font("Meiryo UI",10);_readingFonts.Add(baseFont);Font=baseFont;
        BackColor=Color.FromArgb(242,245,249);Padding=new Padding(6);
        toolbar.BackColor=Color.White;toolbar.Padding=new Padding(6);toolbar.Margin=new Padding(0,0,0,12);
        _openWeb.Visible=false;
        _tabs.Font=baseFont;_tabs.DrawMode=TabDrawMode.OwnerDrawFixed;_tabs.SizeMode=TabSizeMode.Fixed;
        _tabs.ItemSize=new Size(170,35);_tabs.Padding=new Point(14,8);
        _tabs.DrawItem+=(_,e)=>{
            bool selected=e.Index==_tabs.SelectedIndex;var r=e.Bounds;r.Inflate(-3,-3);
            using var brush=new SolidBrush(selected?Accent:Color.FromArgb(232,238,245));e.Graphics.FillRectangle(brush,r);
            TextRenderer.DrawText(e.Graphics,_tabs.TabPages[e.Index].Text,baseFont,r,selected?Color.White:Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        };
        foreach(TabPage page in _tabs.TabPages){page.BackColor=Color.White;page.Padding=new Padding(6);StyleControls(page);}
        StyleControls(toolbar);
        var less=new Button{Text="文字 −",AutoSize=true,AccessibleName="一覧の文字を小さく"};
        var more=new Button{Text="文字 ＋",AutoSize=true,AccessibleName="一覧の文字を大きく"};toolbar.Controls.AddRange([less,more]);StyleControls(less);StyleControls(more);
        less.Click+=(_,_)=>ApplyReadingSize(_readingSize-1,true);more.Click+=(_,_)=>ApplyReadingSize(_readingSize+1,true);
        try{string file=Path.Combine(RecommendationHome,"reading-size.txt");if(File.Exists(file)&&float.TryParse(File.ReadAllText(file),out float size))_readingSize=Math.Clamp(size,10,18);}catch(IOException){}
        foreach(var grid in new[]{_nativeReservations,_nativeGuide,_nativeKeywords,_disabledReservations}){
            grid.EnableHeadersVisualStyles=false;grid.GridColor=Color.FromArgb(224,231,239);grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(230,237,246);grid.ColumnHeadersDefaultCellStyle.ForeColor=Ink;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.FromArgb(230,237,246);grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Ink;
            grid.DefaultCellStyle.BackColor=Color.White;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(245,248,252);grid.DefaultCellStyle.ForeColor=Ink;
            grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(214,232,252);grid.DefaultCellStyle.SelectionForeColor=Color.FromArgb(12,45,81);
            grid.DefaultCellStyle.Padding=new Padding(6,2,6,2);grid.ShowCellToolTips=false;
            grid.CellMouseEnter+=(_,e)=>{
                if(e.RowIndex<0||e.ColumnIndex<0)return;var cell=grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                string text=GetProgramHoverText(grid,e.RowIndex,e.ColumnIndex);
                QueueLargeTip(grid,text,grid.PointToClient(Cursor.Position));
            };
            grid.CellMouseLeave+=(_,_)=>HideLargeTip();grid.Scroll+=(_,_)=>HideLargeTip();grid.MouseDown+=(_,_)=>HideLargeTip();
        }
        _largeTip.Popup+=(_,e)=>{
            using var graphics=e.AssociatedControl.CreateGraphics();int width=Math.Min((int)(680*DeviceDpi/96f),Screen.FromControl(this).WorkingArea.Width-60);
            var measured=graphics.MeasureString(_largeTip.GetToolTip(e.AssociatedControl),_hoverFont,width-36);
            e.ToolTipSize=new Size(Math.Min(width,(int)Math.Ceiling(measured.Width)+36),(int)Math.Ceiling(measured.Height)+32);
        };
        _largeTip.Draw+=(_,e)=>{
            using var background=new SolidBrush(Color.FromArgb(255,253,240));using var border=new Pen(Color.FromArgb(91,113,139));using var ink=new SolidBrush(Ink);
            e.Graphics.FillRectangle(background,e.Bounds);e.Graphics.DrawRectangle(border,0,0,e.Bounds.Width-1,e.Bounds.Height-1);
            e.Graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            e.Graphics.DrawString(e.ToolTipText,_hoverFont,ink,new RectangleF(18,14,e.Bounds.Width-36,e.Bounds.Height-28));
        };
        _hoverDelay.Tick+=(_,_)=>{_hoverDelay.Stop();if(_hoverControl==null||_hoverText.Length==0)return;_largeTip.SetToolTip(_hoverControl,_hoverText);_largeTip.Show(_hoverText,_hoverControl,new Point(_hoverPoint.X+14,_hoverPoint.Y+26),25000);};
        _tabs.SelectedIndexChanged+=(_,_)=>HideLargeTip();Deactivate+=(_,_)=>HideLargeTip();
        ApplyReadingSize(_readingSize,false);
    }
    void StyleControls(Control root)
    {
        if(root is Button button){button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;button.BackColor=button==_recommend?Accent:Color.FromArgb(231,238,247);button.ForeColor=button==_recommend?Color.White:Ink;button.Padding=new Padding(9,4,9,4);button.Margin=new Padding(4,3,8,3);button.Cursor=Cursors.Hand;button.MinimumSize=new Size(0,33);}
        if(root is CheckBox check){check.ForeColor=Ink;check.Margin=new Padding(8,6,10,4);}
        if(root is FlowLayoutPanel flow){flow.BackColor=Color.White;flow.Padding=new Padding(4,4,4,5);}
        foreach(Control child in root.Controls)StyleControls(child);
    }
    internal static Color StationColor(string station)
    {
        double hue=station switch {"NHK"=>20,"日テレ"=>75,"テレ朝"=>305,"TBS"=>260,"テレ東"=>175,"フジ"=>50,"Eテレ"=>125,"MX1" or "MX"=>220,_=>-1};if(hue<0)return Ink;
        // Equal perceptual lightness and chroma in CIELAB; only hue varies.
        double angle=hue*Math.PI/180,fy=(48d+16)/116,fx=fy+28*Math.Cos(angle)/500,fz=fy-28*Math.Sin(angle)/200;
        static double Inv(double t)=>t>6d/29?t*t*t:3*Math.Pow(6d/29,2)*(t-4d/29);
        double x=.95047*Inv(fx),y=Inv(fy),z=1.08883*Inv(fz);
        static int Srgb(double c){double value=c<=.0031308?12.92*c:1.055*Math.Pow(c,1/2.4)-.055;return (int)Math.Round(Math.Clamp(value,0,1)*255);}
        return Color.FromArgb(Srgb(3.2404542*x-1.5371385*y-.4985314*z),Srgb(-.969266*x+1.8760108*y+.041556*z),Srgb(.0556434*x-.2040259*y+1.0572252*z));
    }
    internal string GetProgramHoverText(DataGridView grid,int rowIndex,int columnIndex)
    {
        var row=grid.Rows[rowIndex];
        TsRecommend.Show? show=row.Tag as TsRecommend.Show;
        string word="";AiEntry? ai=null;
        if(row.Tag is TsRecommend.Booking booking){show=_nativeShows.FirstOrDefault(s=>TsRecommend.Planner.SameBroadcast(s,booking));word=Convert.ToString(row.Cells[4].Value)??"";ai=row.Cells[4].Tag as AiEntry;}
        if(show!=null||grid==_nativeReservations){
            var parts=new List<string>();
            parts.Add(!string.IsNullOrWhiteSpace(show?.Description)?show.Description:"番組説明を取得できていません。番組表を更新してください。");
            if(ai!=null)parts.Add("AIおすすめの理由\n"+ai.Reason);
            if(!string.IsNullOrWhiteSpace(word)&&word!="手動")parts.Add("検索ワード："+word);
            return string.Join("\n\n",parts);
        }
        return string.IsNullOrWhiteSpace(row.Cells[columnIndex].ToolTipText)?Convert.ToString(row.Cells[columnIndex].FormattedValue)??"":row.Cells[columnIndex].ToolTipText;
    }
    void QueueLargeTip(Control control,string text,Point point){HideLargeTip();_hoverControl=control;_hoverText=text;_hoverPoint=point;if(text.Length>0)_hoverDelay.Start();}
    void HideLargeTip(){_hoverDelay.Stop();if(_hoverControl!=null){_largeTip.Hide(_hoverControl);_largeTip.SetToolTip(_hoverControl,null);}_hoverControl=null;}
    void ApplyReadingSize(float size,bool save)
    {
        _readingSize=Math.Clamp(size,10,18);var regular=new Font("Meiryo UI",_readingSize);var bold=new Font("Meiryo UI",_readingSize,FontStyle.Bold);_readingFonts.Add(regular);_readingFonts.Add(bold);
        var previous=_nativeItalic;_nativeItalic=new Font("Meiryo UI",_readingSize,FontStyle.Bold|FontStyle.Italic);
        foreach(var grid in new[]{_nativeReservations,_nativeGuide,_nativeKeywords,_disabledReservations}){
            grid.DefaultCellStyle.Font=regular;grid.ColumnHeadersDefaultCellStyle.Font=bold;grid.ColumnHeadersHeight=regular.Height+6;grid.RowTemplate.Height=regular.Height+9;
            foreach(DataGridViewRow row in grid.Rows){row.Height=regular.Height+9;if(grid==_nativeReservations&&row.Cells[4].Tag is AiEntry){row.Cells[4].Style.Font=_nativeItalic;row.Cells[5].Style.Font=_nativeItalic;}}
        }
        _nativeReservations.Columns[1].Width=(int)(230*_readingSize/12);_nativeReservations.Columns[2].Width=100;_nativeReservations.Columns[3].Width=100;_nativeReservations.Columns[4].Width=(int)(285*_readingSize/12);
        _nativeGuide.Columns[0].Width=(int)(230*_readingSize/12);_nativeGuide.Columns[1].Width=100;_nativeGuide.Columns[2].Width=100;
        ApplyDatePartitions(_nativeReservations);ApplyDatePartitions(_nativeGuide);
        _schedule.Font=regular;_schedule.RefreshSchedule();
        previous?.Dispose();
        if(save){Directory.CreateDirectory(RecommendationHome);File.WriteAllText(Path.Combine(RecommendationHome,"reading-size.txt"),_readingSize.ToString());}
        Invalidate(true);
    }
    void DisposeReadableDesign(){HideLargeTip();_hoverDelay.Dispose();_largeTip.Dispose();_hoverFont.Dispose();foreach(var font in _readingFonts)font.Dispose();}
}
