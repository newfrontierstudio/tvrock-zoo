using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    internal static string[] DefaultStationOrder(IEnumerable<string> names)=>names.Distinct().OrderBy(StationGroup).ThenBy(n=>Data.StationKey(n),StringComparer.CurrentCulture).ToArray();
    static int StationGroup(string name){var key=Data.StationKey(name);var ground=new[]{"NHK","Eテレ","日本テレビ","テレビ朝日","TBS","テレビ東京","フジテレビ","TOKYOMX"};int i=Array.IndexOf(ground,key);if(i>=0)return i;if(name.Contains("BS",StringComparison.OrdinalIgnoreCase)||name.Contains("ＷＯＷＯＷ")||name.Contains("WOWOW",StringComparison.OrdinalIgnoreCase)||name.Contains("スターチャンネル"))return 100;return 200;}
    string[] OrderedStations(){var defaults=DefaultStationOrder(_nativeShows.Select(s=>s.Station));var file=Path.Combine(RecommendationHome,"station-order.txt");if(!File.Exists(file))return defaults;var saved=File.ReadAllLines(file).Select(s=>defaults.FirstOrDefault(n=>Data.SameStation(n,s))).OfType<string>().Distinct();return saved.Concat(defaults.Where(n=>!saved.Contains(n))).ToArray();}
    void InitializeStationBadgeReordering(FlowLayoutPanel badges)
    {
        void ApplySaved(){string file=Path.Combine(RecommendationHome,"station-order.txt");if(!File.Exists(file))return;int index=0;foreach(string name in File.ReadAllLines(file)){var badge=badges.Controls.Cast<Control>().FirstOrDefault(c=>c.Tag is string station&&Data.SameStation(station,name));if(badge!=null)badges.Controls.SetChildIndex(badge,index++);}}
        ApplySaved();Point down=Point.Empty;
        foreach(Control badge in badges.Controls){badge.AllowDrop=true;badge.MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Left)down=e.Location;};badge.MouseMove+=(_,e)=>{if(e.Button==MouseButtons.Left&&Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)>8&&badge.Tag is string station)badge.DoDragDrop(station,DragDropEffects.Move);};badge.DragEnter+=(_,e)=>{if(e.Data?.GetData(typeof(string)) is string source&&badges.Controls.Cast<Control>().Any(c=>Equals(c.Tag,source)))e.Effect=DragDropEffects.Move;};badge.DragDrop+=(_,e)=>{if(e.Data?.GetData(typeof(string)) is not string source||badge.Tag is not string target)return;var order=ScheduleGrid.MoveStation(badges.Controls.Cast<Control>().Select(c=>(string)c.Tag!),source,target);try{Directory.CreateDirectory(RecommendationHome);File.WriteAllLines(Path.Combine(RecommendationHome,"station-order.txt"),order);for(int i=0;i<order.Count;i++){var item=badges.Controls.Cast<Control>().Single(c=>Equals(c.Tag,order[i]));badges.Controls.SetChildIndex(item,i);}RenderSchedule();}catch(Exception error){MessageBox.Show(this,error.Message,"局の並び順を保存できません");}};}
    }
    void EditStationOrder()
    {
        using var dialog=new Form{Text="番組表の局の並び順",ClientSize=new Size(420,480),Font=Font,StartPosition=FormStartPosition.CenterParent};
        var list=new ListBox{Dock=DockStyle.Fill};list.Items.AddRange(OrderedStations());var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true};var up=new Button{Text="上へ",AutoSize=true};var down=new Button{Text="下へ",AutoSize=true};var save=new Button{Text="保存",AutoSize=true};bar.Controls.AddRange([up,down,save]);dialog.Controls.Add(list);dialog.Controls.Add(bar);StyleControls(bar);
        void Move(int delta){int i=list.SelectedIndex;if(i<0||i+delta<0||i+delta>=list.Items.Count)return;var item=list.Items[i];list.Items.RemoveAt(i);list.Items.Insert(i+delta,item);list.SelectedIndex=i+delta;}
        up.Click+=(_,_)=>Move(-1);down.Click+=(_,_)=>Move(1);save.Click+=(_,_)=>{try{Directory.CreateDirectory(RecommendationHome);File.WriteAllLines(Path.Combine(RecommendationHome,"station-order.txt"),list.Items.Cast<string>());RenderSchedule();dialog.Close();}catch(Exception error){MessageBox.Show(dialog,error.Message);}};dialog.ShowDialog(this);
    }
}
