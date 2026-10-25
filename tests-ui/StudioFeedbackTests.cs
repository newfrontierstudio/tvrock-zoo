using System.Reflection;
using TsScheduler;
using TsRecommend;
internal static class StudioFeedbackTests {
 internal static void Run(MainForm form){
  Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory,"work","zoo120-feedback"));
  if(!Desc(form).OfType<Button>().Any(b=>b.Text=="TVRockの接続設定"&&!b.IsDisposed))throw new Exception("TVRock connection settings disappeared");
  var nav=Desc(form).OfType<ListBox>().FirstOrDefault(b=>b.DrawMode==DrawMode.OwnerDrawFixed);if(nav==null||nav.Items.Count<5)throw new Exception("Modern settings navigation missing");
  if(nav.SelectedIndex<0)throw new Exception("Initial settings page disappeared");int selected=nav.SelectedIndex;form.ClearAllTabSelections();if(nav.SelectedIndex!=selected)throw new Exception("Header deselection cleared settings content");
  CheckStationColorDialog(form);
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;var tabs=(TabControl)typeof(MainForm).GetField("_tabs",flags)!.GetValue(form)!;var page=tabs.TabPages.Cast<TabPage>().First(p=>p.Text=="詳細設定");tabs.SelectedTab=page;Application.DoEvents();
  using(var shot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(Environment.CurrentDirectory,"work","zoo120-feedback","settings.png"));}
  Console.WriteLine("PASS: Studio feedback - modern settings retains TVRock connection; exact live color preview, Cancel restoration and Apply persistence");
 }
 static IEnumerable<Control> Desc(Control root)=>root.Controls.Cast<Control>().SelectMany(child=>new[]{child}.Concat(Desc(child)));
    internal static void CheckStationColorDialog(MainForm form)
    {
        var badge=Desc(form).OfType<StationColorButton>().First(b=>Data.StationKey((string)b.Tag!)=="NHK");
        string key=Data.StationKey((string)badge.Tag!);var options=TsScheduler.DisplayOptions.Current;bool had=options.StationColors.TryGetValue(key,out int before),exact=options.ExactStationColors.Contains(key);Color display=badge.BackColor;
        string? path=TsScheduler.DisplayOptions.TestPath;TsScheduler.DisplayOptions.TestPath=Path.Combine(Environment.CurrentDirectory,"work","zoo120-feedback","color-transaction.json");
        var choose=Desc(badge.Parent!.Parent!).OfType<Button>().First(b=>b.Text=="色を選ぶ");
        void Edit(DialogResult result,Color color){Exception? failure=null;using var timer=new System.Windows.Forms.Timer{Interval=100};timer.Tick+=(_,_)=>{var picker=Application.OpenForms.OfType<StudioColorPicker>().FirstOrDefault();if(picker==null)return;timer.Stop();try{picker.SetColor(color);if(badge.BackColor.ToArgb()!=color.ToArgb()||TsScheduler.StationBadgeRenderer.Background((string)badge.Tag!).ToArgb()!=color.ToArgb())throw new Exception("Station badge did not preview exact picker color: "+badge.Tag+" badge="+badge.BackColor+" render="+TsScheduler.StationBadgeRenderer.Background((string)badge.Tag!)+" expected="+color+" selected="+string.Join(";",options.StationColors.Keys));}catch(Exception e){failure=e;}picker.DialogResult=result;picker.Close();};timer.Start();typeof(Button).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(choose,[EventArgs.Empty]);if(failure!=null)throw failure;}
        try{Edit(DialogResult.Cancel,Color.Coral);if(badge.BackColor.ToArgb()!=display.ToArgb()||options.StationColors.ContainsKey(key)!=had||had&&options.StationColors[key]!=before||options.ExactStationColors.Contains(key)!=exact)throw new Exception("Cancel did not restore prior station color: before="+display+" now="+badge.BackColor+" had="+had+" present="+options.StationColors.ContainsKey(key)+" exact="+exact+" nowexact="+options.ExactStationColors.Contains(key));Edit(DialogResult.OK,Color.CornflowerBlue);if(badge.BackColor.ToArgb()!=Color.CornflowerBlue.ToArgb()||!File.Exists(TsScheduler.DisplayOptions.TestPath))throw new Exception("Picker apply was not saved");}
        finally{if(had)options.StationColors[key]=before;else options.StationColors.Remove(key);if(exact)options.ExactStationColors.Add(key);else options.ExactStationColors.Remove(key);TsScheduler.DisplayOptions.TestPath=path;}
    }
}