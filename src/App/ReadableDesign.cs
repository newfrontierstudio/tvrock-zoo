using System.Drawing.Drawing2D;
namespace TsScheduler;
internal sealed partial class MainForm
{
    readonly FlatHoverCard _largeTip = new();
    readonly System.Windows.Forms.Timer _hoverDelay = new() { Interval=450 };
    readonly Font _hoverFont = new("Meiryo UI",10.8f,FontStyle.Regular);
    readonly Font _hoverKeywordFont = new("Meiryo UI",10.8f,FontStyle.Bold);
    Control? _hoverControl;
    string _hoverText="";
    Point _hoverPoint;
    float _readingSize=10;
    readonly List<Font> _readingFonts=[];
    static readonly Color Ink=Color.FromArgb(23,42,62);
    static readonly Color Accent=Color.FromArgb(67,78,158);
    void InitializeReadableDesign(FlowLayoutPanel toolbar)
    {
        var baseFont=new Font("Meiryo UI",10);_readingFonts.Add(baseFont);Font=baseFont;
        BackColor=Color.FromArgb(242,245,249);Padding=new Padding(6);
        toolbar.BackColor=Color.White;toolbar.Padding=new Padding(6);toolbar.Margin=new Padding(0,0,0,12);
        _openWeb.Visible=false;
        _tabs.Font=baseFont;_tabs.DrawMode=TabDrawMode.OwnerDrawFixed;_tabs.SizeMode=TabSizeMode.Fixed;
        _tabs.ItemSize=new Size(170,35);_tabs.Padding=new Point(14,8);
        _tabs.DrawItem+=(_,e)=>{
            bool selected=e.Index==_tabs.SelectedIndex;var r=e.Bounds;r.Inflate(-3,0);r.Y+=3;r.Height-=3;
            using var brush=new SolidBrush(selected?Accent:Color.FromArgb(220,225,235));using(var shape=FlatTabControl.TabShape(r,6*DeviceDpi/96f)){var previous=e.Graphics.SmoothingMode;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.FillPath(brush,shape);e.Graphics.SmoothingMode=previous;}
            var textBounds=r;
            if(e.Index==0){var arrow=ReservationArrowBounds(e.Bounds,DeviceDpi);textBounds.Width=Math.Max(1,arrow.Left-textBounds.Left);using(var divider=new SolidBrush(Color.White))e.Graphics.FillRectangle(divider,arrow.Left,r.Top+3,1,Math.Max(1,r.Height-6));TextRenderer.DrawText(e.Graphics,"▼",baseFont,arrow,selected?Color.White:Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
            TextRenderer.DrawText(e.Graphics,_tabs.TabPages[e.Index].Text,baseFont,textBounds,selected?Color.White:Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        };
        foreach(TabPage page in _tabs.TabPages){page.BackColor=Color.White;page.Padding=new Padding(6);StyleControls(page);}
        StyleControls(toolbar);
        var less=new Button{Text="文字 −",AutoSize=true,AccessibleName="一覧の文字を小さく"};
        var more=new Button{Text="文字 ＋",AutoSize=true,AccessibleName="一覧の文字を大きく"};toolbar.Controls.AddRange([less,more]);StyleControls(less);StyleControls(more);
        less.Click+=(_,_)=>ApplyReadingSize(_readingSize-1,true);more.Click+=(_,_)=>ApplyReadingSize(_readingSize+1,true);
        try{string file=Path.Combine(RecommendationHome,"reading-size.txt");if(File.Exists(file)&&float.TryParse(File.ReadAllText(file),out float size))_readingSize=Math.Clamp(size,10,18);}catch(IOException){}
        foreach(var grid in new[]{_nativeReservations,_nativeGuide,_nativeKeywords,_disabledReservations}){
            ModernScrollBar.Attach(grid);
            grid.EnableHeadersVisualStyles=false;grid.GridColor=Color.FromArgb(224,231,239);grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(220,225,235);grid.ColumnHeadersDefaultCellStyle.ForeColor=Ink;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.FromArgb(220,225,235);grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Ink;
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
        _hoverDelay.Tick+=(_,_)=>{
            _hoverDelay.Stop();if(_hoverControl==null||_hoverText.Length==0)return;
            var owner=_hoverControl;var text=_hoverText;using var graphics=owner.CreateGraphics();int width=Math.Min((int)(680*DeviceDpi/96f),Screen.FromControl(this).WorkingArea.Width-60);var measured=LayoutHoverText(graphics,text,_hoverFont,_hoverKeywordFont,width-36,PointF.Empty);int actualWidth=Math.Min(width,Math.Max(100,(int)Math.Ceiling(measured.Width)+40));measured=LayoutHoverText(graphics,text,_hoverFont,_hoverKeywordFont,actualWidth-36,PointF.Empty);
            _largeTip.ShowCard(owner,owner.PointToScreen(new Point(_hoverPoint.X+14,_hoverPoint.Y+26)),new Size(actualWidth,(int)Math.Ceiling(measured.Height)+24),(g,bounds)=>{using var ink=new SolidBrush(Ink);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;LayoutHoverText(g,text,_hoverFont,_hoverKeywordFont,bounds.Width-36,new PointF(18,10),ink);});
        };
        _tabs.SelectedIndexChanged+=(_,_)=>HideLargeTip();Deactivate+=(_,_)=>HideLargeTip();
        ApplyReadingSize(_readingSize,false);
    }
    internal static SizeF LayoutHoverText(Graphics graphics,string text,Font body,Font keyword,float width,PointF origin,Brush? ink=null)
    {
        width=Math.Max(40,width);float y=0,maxWidth=0;int lineIndex=0;bool guideHeader=System.Text.RegularExpressions.Regex.IsMatch(text,@"^.+\d{2}/\d{2} \d{2}:\d{2}");
        foreach(string line in text.Replace("\r","").Split('\n')){
            int index=lineIndex++;if(line.Length==0){y+=body.GetHeight(graphics)*.5f;continue;}var lineFont=(guideHeader&&index<2)||line.StartsWith("AIおすすめ")?keyword:body;
            const string label="検索ワード：";
            if(line.StartsWith(label,StringComparison.Ordinal)){
                float prefix=Math.Min(width-20,graphics.MeasureString(label,body).Width);
                string word=line[label.Length..].TrimStart();var measured=graphics.MeasureString(word.Length==0?" ":word,keyword,(int)Math.Max(20,width-prefix));
                if(ink!=null){graphics.DrawString(label,body,ink,new PointF(origin.X,origin.Y+y));graphics.DrawString(word,keyword,ink,new RectangleF(origin.X+prefix,origin.Y+y,width-prefix,measured.Height+2));}
                maxWidth=Math.Max(maxWidth,prefix+measured.Width);y+=Math.Max(body.GetHeight(graphics),measured.Height);
            }else{
                string remaining=line;
                using var format=new StringFormat(StringFormat.GenericTypographic){FormatFlags=StringFormatFlags.MeasureTrailingSpaces};
                while(remaining.Length>0){
                    int low=1,high=remaining.Length;
                    while(low<high){int mid=(low+high+1)/2;if(graphics.MeasureString(remaining[..mid],lineFont,int.MaxValue,format).Width<=width)low=mid;else high=mid-1;}
                    int count=low;if(count<remaining.Length&&count>1&&char.IsHighSurrogate(remaining[count-1]))count--;
                    string part=remaining[..count];float h=lineFont.GetHeight(graphics)*1.2f;
                    if(ink!=null)graphics.DrawString(part,lineFont,ink,new PointF(origin.X,origin.Y+y),format);
                    maxWidth=Math.Max(maxWidth,graphics.MeasureString(part,lineFont,int.MaxValue,format).Width);y+=h;remaining=remaining[count..];
                }
            }
        }
        return new SizeF(Math.Min(width,maxWidth),y);
    }
    void StyleControls(Control root)
    {
        if(root is Button button){RoundedControls.Apply(button);button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;button.BackColor=button==_recommend?Accent:(button.Text is "一覧を更新" or "文字 −" or "文字 ＋" or "元に戻す"?Color.FromArgb(237,239,242):Color.FromArgb(231,238,247));button.ForeColor=button==_recommend?Color.White:Ink;button.Padding=new Padding(9,4,9,4);button.Margin=new Padding(4,3,8,3);button.Cursor=Cursors.Hand;button.MinimumSize=new Size(0,33);}
        if(root is CheckBox check){check.ForeColor=Ink;check.Margin=new Padding(8,6,10,4);}
        if(root is FlowLayoutPanel flow){flow.BackColor=Color.White;flow.Padding=new Padding(4,4,4,5);}
        foreach(Control child in root.Controls)StyleControls(child);
    }
    internal static Color StationColor(string station)
    {
        double hue=station switch {"NHK"=>20,"日テレ"=>85,"テレ朝"=>315,"TBS"=>270,"テレ東"=>180,"フジ"=>55,"Eテレ"=>140,"MX1" or "MX"=>230,_=>-1};if(hue<0)return Ink;
        // CIELAB lightness stays equal. Stronger green/violet separation; red chroma is reduced to balance visual prominence.
        double chroma=station=="NHK"?23:38;
        double angle=hue*Math.PI/180,fy=(48d+16)/116,fx=fy+chroma*Math.Cos(angle)/500,fz=fy-chroma*Math.Sin(angle)/200;
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
            if(row.Tag is TsRecommend.Booking timed){var margins=RecordingTiming.Current(timed);parts.Add($"録画時間：開始 {margins.Head:+0;-0;0}秒・終了 {margins.Tail:+0;-0;0}秒");}
            if(ai!=null)parts.Add("AIおすすめの理由\n"+ai.Reason);
            if(!string.IsNullOrWhiteSpace(word)&&word!="手動")parts.Add("検索ワード："+word);
            return string.Join("\n\n",parts);
        }
        return string.IsNullOrWhiteSpace(row.Cells[columnIndex].ToolTipText)?Convert.ToString(row.Cells[columnIndex].FormattedValue)??"":row.Cells[columnIndex].ToolTipText;
    }
    void QueueLargeTip(Control control,string text,Point point){HideLargeTip();_hoverControl=control;_hoverText=text;_hoverPoint=point;if(text.Length>0)_hoverDelay.Start();}
    void HideLargeTip(){_hoverDelay.Stop();_largeTip.HideCard();_hoverControl=null;}
    void ApplyReadingSize(float size,bool save)
    {
        _readingSize=Math.Clamp(size,10,18);var regular=new Font("Meiryo UI",_readingSize);var bold=new Font("Meiryo UI",_readingSize,FontStyle.Bold);_readingFonts.Add(regular);_readingFonts.Add(bold);
        var previous=_nativeItalic;_nativeItalic=new Font("Meiryo UI",_readingSize,FontStyle.Bold|FontStyle.Italic);
        foreach(var grid in new[]{_nativeReservations,_nativeGuide,_nativeKeywords,_disabledReservations}){
            grid.DefaultCellStyle.Font=regular;grid.ColumnHeadersDefaultCellStyle.Font=bold;grid.ColumnHeadersHeight=regular.Height+6;grid.RowTemplate.Height=regular.Height+5;
            foreach(DataGridViewRow row in grid.Rows){row.Height=regular.Height+5;if(grid==_nativeReservations&&row.Cells[4].Tag is AiEntry){row.Cells[4].Style.Font=_nativeItalic;row.Cells[5].Style.Font=_nativeItalic;}}
        }
        _nativeReservations.Columns[1].Width=(int)(230*_readingSize/12);_nativeReservations.Columns[2].Width=100;_nativeReservations.Columns[3].Width=100;_nativeReservations.Columns[4].Width=(int)(285*_readingSize/12);
        _nativeGuide.Columns[0].Width=(int)(230*_readingSize/12);_nativeGuide.Columns[1].Width=100;_nativeGuide.Columns[2].Width=100;
        ApplyDatePartitions(_nativeReservations);ApplyDatePartitions(_nativeGuide);
        _schedule.Font=regular;_schedule.RefreshSchedule();
        previous?.Dispose();
        if(save){Directory.CreateDirectory(RecommendationHome);File.WriteAllText(Path.Combine(RecommendationHome,"reading-size.txt"),_readingSize.ToString());}
        Invalidate(true);
    }
    void DisposeReadableDesign(){HideLargeTip();_hoverDelay.Dispose();_largeTip.Dispose();_hoverFont.Dispose();_hoverKeywordFont.Dispose();foreach(var font in _readingFonts)font.Dispose();}
}
