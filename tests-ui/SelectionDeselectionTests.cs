using System.Reflection;
using TsRecommend;
using TsScheduler;
namespace TsScheduler.Tests;
internal static class SelectionDeselectionTests
{
    internal static void Run(MainForm form)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var tabs=(TabControl)typeof(MainForm).GetField("_tabs",flags)!.GetValue(form)!;
        using var page=new TabPage("選択解除試験");
        using var grid=new DataGridView{Size=new Size(300,200),AllowUserToAddRows=false};grid.Columns.Add("value","value");grid.Rows.Add("row");
        using var button=new Button();using var input=new TextBox();using var blank=new Panel();
        using var options=new CheckedListBox();options.Items.Add("使用する局",true);
        using var library=new RecordedMediaView(()=>Path.GetTempPath(),()=>Path.GetTempPath());
        var selection=(HashSet<string>)typeof(RecordedMediaView).GetField("selected",flags)!.GetValue(library)!;selection.Add("synthetic.ts");
        var schedule=new ScheduleGrid();var now=DateTimeOffset.Now;schedule.Selected=new Show("synthetic",1,1,"局","番組","",now,now.AddHours(1),[]);
        page.Controls.AddRange([grid,button,input,blank,options,library,schedule]);tabs.TabPages.Add(page);
        try{
            grid.CurrentCell=grid.Rows[0].Cells[0];grid.Rows[0].Selected=true;
            using var host=new Form{ClientSize=new Size(320,220),ShowInTaskbar=false};host.Controls.Add(grid);host.Show();Application.DoEvents();var cell=grid.GetCellDisplayRectangle(0,0,false);
            if(!MainForm.IsSelectionBlank(grid,new Point(10,grid.ClientSize.Height-5))||MainForm.IsSelectionBlank(grid,new Point(cell.Left+cell.Width/2,cell.Top+cell.Height/2)))throw new Exception($"Only grid background should deselect: size={grid.ClientSize}, cell={cell}, bottom={grid.HitTest(10,grid.ClientSize.Height-5).Type}, row={grid.HitTest(cell.Left+cell.Width/2,cell.Top+cell.Height/2).Type}");
            if(!MainForm.IsSelectionBlank(blank,Point.Empty)||MainForm.IsSelectionBlank(button,Point.Empty)||MainForm.IsSelectionBlank(input,Point.Empty)||MainForm.IsSelectionBlank(options,new Point(10,100)))throw new Exception("Buttons, text input and checked settings must preserve selection");
            using var card=new Panel{Tag=(new RecordedFile("synthetic.ts","file",DateTime.Today,1,false),new CheckBox())};
            if(MainForm.IsSelectionBlank(card,Point.Empty))throw new Exception("A media card is a selectable item, not blank space");
            host.Controls.Remove(grid);page.Controls.Add(grid);host.Hide();
            form.ClearAllTabSelections();
            if(grid.SelectedRows.Count!=0||grid.CurrentCell!=null||schedule.Selected!=null||selection.Count!=0||!options.GetItemChecked(0))throw new Exception("All tabs must clear selection without changing checked settings");
        }finally{tabs.TabPages.Remove(page);}
    }
}
