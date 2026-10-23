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
    static Label DetailNote(string text)=>new(){Text=text,AutoSize=true,MaximumSize=new Size(740,0),ForeColor=Color.FromArgb(85,98,108),Margin=new Padding(0,2,0,10)};
    FlowLayoutPanel DetailSection(FlowLayoutPanel parent,string title,string description)
    {
        var section=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0,12,0,12),Margin=new Padding(0,8,0,12)};
        section.Controls.Add(new Label{Text=title,AutoSize=true,Font=new Font(Font,FontStyle.Bold),ForeColor=Ink,Margin=new Padding(0,0,0,8)});
        section.Controls.Add(DetailNote(description));parent.Controls.Add(section);
        if(detailIndex!=null){var button=new Button{Text=title,AutoSize=false,Width=170,Height=38,TextAlign=ContentAlignment.MiddleCenter};button.Click+=(_,_)=>_detailLanding.ScrollControlIntoView(section);detailIndex.Controls.Add(button);}return section;
    }
}
