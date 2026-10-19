using TsRecommend;
namespace TsScheduler;
internal sealed class StationColorButton:Button
{
    internal StationColorButton(){SetStyle(ControlStyles.StandardClick|ControlStyles.StandardDoubleClick,true); }
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent?.BackColor??Color.White);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;const int inset=20;var bounds=Rectangle.Inflate(ClientRectangle,-inset,-inset);if(FlatAppearance.BorderSize>0){using var outer=RoundedControls.Shape(new Rectangle(0,0,Width-1,Height-1),8);using var border=new SolidBrush(FlatAppearance.BorderColor);e.Graphics.FillPath(border,outer);}using var path=RoundedControls.Shape(bounds,4*DeviceDpi/96f);using var fill=new SolidBrush(BackColor);e.Graphics.FillPath(fill,path);using var small=new Font(Font.FontFamily,Font.SizeInPoints*.85f,FontStyle.Regular,GraphicsUnit.Point);TextRenderer.DrawText(e.Graphics,Text,small,bounds,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);}

}
internal sealed partial class MainForm
{
    void InitializeStationColorEditor(FlowLayoutPanel body)
    {
        var tools=new FlowLayoutPanel{AutoSize=true};var selectedLabel=new Label{Text="局名の背景色",AutoSize=false,Width=260,Height=38,AutoEllipsis=false,TextAlign=ContentAlignment.MiddleLeft,Padding=Padding.Empty,Margin=Padding.Empty};var choose=new Button{Text="色を選ぶ",AutoSize=true};var reset=new Button{Text="標準色に戻す",AutoSize=true};tools.Controls.AddRange([selectedLabel,choose,reset]);body.Controls.Add(tools);StyleControls(tools);
        var badges=new FlowLayoutPanel{AutoSize=true,WrapContents=true,Margin=new Padding(0,8,0,8)};body.Controls.Add(badges);
        IEnumerable<string> configured=[];try{configured=Data.Tuners(Path.Combine(RockFolder,"dtv.ini")).Stations.Values;}catch(IOException){}catch(UnauthorizedAccessException){}
        var names=configured.Concat(new[]{"NHK","日テレ","テレ朝","TBS","テレ東","フジ","Eテレ","MX"}).DistinctBy(Data.StationKey).ToArray();string selected=names.FirstOrDefault(n=>Data.StationKey(n)=="NHK")??names[0];
        selectedLabel.Width=Math.Max(300,names.Max(n=>TextRenderer.MeasureText("局名の背景色　編集中："+n,selectedLabel.Font).Width)+12);selectedLabel.Height=choose.PreferredSize.Height+choose.Margin.Vertical;
        void Fit(){int width=Math.Max(240,_detailLanding.ClientSize.Width-60);badges.MaximumSize=new Size(width,0);badges.Width=width;}body.SizeChanged+=(_,_)=>Fit();Fit();
        void Preview(){selectedLabel.Text="局名の背景色　編集中："+selected;foreach(Button badge in badges.Controls){string station=(string)badge.Tag!;badge.BackColor=StationBadgeRenderer.Background(station);badge.ForeColor=StationBadgeRenderer.Foreground(badge.BackColor);bool active=Data.SameStation(station,selected);badge.FlatAppearance.BorderSize=active?20:0;badge.FlatAppearance.BorderColor=Color.FromArgb(202,207,212);badge.AccessibleName=station+(active?" 編集中":" 選択して色を編集");}}
        void Change(Color color){try{DisplayOptions.Current.StationColors[Data.StationKey(selected)]=color.ToArgb();DisplayOptions.Current.Save();Preview();ApplyDisplayOptions();}catch(Exception error){MessageBox.Show(this,error.Message,"色を保存できません");}}
        foreach(string station in names){var badge=new StationColorButton{Text=station=="MX1"?"MX":station,Tag=station,AutoSize=false,Width=124,Height=74,AutoEllipsis=true};badges.Controls.Add(badge);StyleControls(badge);badge.Click+=(_,_)=>{selected=station;Preview();};badge.DoubleClick+=(_,_)=>{selected=station;Preview();choose.PerformClick();};}
        InitializeStationBadgeReordering(badges);
        choose.Click+=(_,_)=>{using var dialog=new ColorDialog{Color=StationBadgeRenderer.Background(selected),FullOpen=true};if(dialog.ShowDialog(this)==DialogResult.OK)Change(dialog.Color);};
        reset.Click+=(_,_)=>{try{DisplayOptions.Current.StationColors.Remove(Data.StationKey(selected));DisplayOptions.Current.Save();Preview();ApplyDisplayOptions();}catch(Exception error){MessageBox.Show(this,error.Message);}};
        Preview();
    }
}
