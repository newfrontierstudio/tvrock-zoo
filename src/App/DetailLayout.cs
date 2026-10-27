namespace TsScheduler;
internal sealed partial class MainForm
{
    FlowLayoutPanel? detailIndex;
    void FitDetailIndex()
    {
        if(detailIndex==null||detailIndex.Controls.Count==0)return;
        int available=Math.Max(240,_detailLanding.ClientSize.Width-48),preferred=Math.Max(170,TextRenderer.MeasureText("起動・アップデート",Font).Width+36);
        int count=detailIndex.Controls.Count,maxColumns=Math.Max(1,available/(preferred+12)),rows=(int)Math.Ceiling((double)count/maxColumns),columns=(int)Math.Ceiling((double)count/rows),width=Math.Min(preferred,available-20);
        foreach(Control button in detailIndex.Controls){button.AutoSize=false;button.Width=width;button.Height=Math.Max(38,Font.Height+18);}
        detailIndex.MaximumSize=new Size(columns*(width+12)+8,0);
    }
    void BuildModernDetails(FlowLayoutPanel oldBody)
    {
        var sections=oldBody.Controls.OfType<FlowLayoutPanel>().Where(c=>c!=detailIndex).ToArray();
        var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,BackColor=Color.FromArgb(246,248,250),Padding=new Padding(18),Margin=Padding.Empty};
        shell.ColumnStyles.Add(new(SizeType.Absolute,190));shell.ColumnStyles.Add(new(SizeType.Percent,100));shell.RowStyles.Add(new(SizeType.Absolute,64));shell.RowStyles.Add(new(SizeType.Percent,100));
        var heading=new Label{Text="設定",Font=new Font("Yu Gothic UI",18,FontStyle.Bold),Dock=DockStyle.Fill,ForeColor=Color.FromArgb(28,37,46),Padding=new Padding(0,5,0,0)};shell.Controls.Add(heading,0,0);
        shell.Controls.Add(new Label{Text="変更は自動保存されます",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(105,115,125)},1,0);
        var nav=new ListBox{AccessibleName="設定メニュー",Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=shell.BackColor,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=46,IntegralHeight=false};
        var content=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(22,12,22,12),Margin=new Padding(16,0,0,0)};
        shell.Controls.Add(nav,0,1);shell.Controls.Add(content,1,1);
        var pages=new List<ChromeScrollPanel>();
        foreach(var section in sections){oldBody.Controls.Remove(section);var title=section.Controls.OfType<Label>().First();nav.Items.Add(title.Text);title.Font=new Font("Yu Gothic UI",15,FontStyle.Bold);title.Margin=new Padding(0,0,0,12);section.Padding=new Padding(0);section.Margin=Padding.Empty;section.Dock=DockStyle.Top;var page=new ChromeScrollPanel{Dock=DockStyle.Fill,AutoScroll=true,Visible=false,BackColor=Color.White};page.Controls.Add(section);content.Controls.Add(page);pages.Add(page);}
        nav.DrawItem+=(_,e)=>{if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;using var bg=new SolidBrush(selected?Color.FromArgb(222,239,241):nav.BackColor);e.Graphics.FillRectangle(bg,e.Bounds);if(selected){using var line=new SolidBrush(Color.FromArgb(32,125,137));e.Graphics.FillRectangle(line,e.Bounds.Left,e.Bounds.Top+9,3,e.Bounds.Height-18);}TextRenderer.DrawText(e.Graphics,nav.Items[e.Index].ToString(),nav.Font,Rectangle.Inflate(e.Bounds,-14,0),Color.FromArgb(35,48,59),TextFormatFlags.Left|TextFormatFlags.VerticalCenter);};
        nav.SelectedIndexChanged+=(_,_)=>{for(int i=0;i<pages.Count;i++)pages[i].Visible=i==nav.SelectedIndex;if(nav.SelectedIndex>=0){pages[nav.SelectedIndex].AutoScrollPosition=Point.Empty;pages[nav.SelectedIndex].BringToFront();}};
        _detailLanding.Controls.Remove(oldBody);oldBody.Dispose();detailIndex=null;_detailLanding.AutoScroll=false;_detailLanding.Controls.Add(shell);if(nav.Items.Count>0)nav.SelectedIndex=0;
    }
    static Label DetailNote(string text)=>new(){Text=text,AutoSize=true,MaximumSize=new Size(740,0),ForeColor=Color.FromArgb(85,98,108),Margin=new Padding(0,2,0,10)};
    FlowLayoutPanel DetailSection(FlowLayoutPanel parent,string title,string description)
    {
        var section=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0,12,0,12),Margin=new Padding(0,8,0,12)};
        section.Controls.Add(new Label{Text=title,AutoSize=true,Font=new Font(Font,FontStyle.Bold),ForeColor=Ink,Margin=new Padding(0,0,0,8)});
        section.Controls.Add(DetailNote(description));parent.Controls.Add(section);
        if(detailIndex!=null){var button=new Button{Text=title,AutoSize=false,Width=170,Height=38,TextAlign=ContentAlignment.MiddleCenter};button.Click+=(_,_)=>_detailLanding.ScrollControlIntoView(section);detailIndex.Controls.Add(button);}return section;
    }
}
