using System.Text.Json;
namespace TsScheduler;
internal sealed record GestureOptions
{
    public bool Enabled{get;init;}
    public Dictionary<string,string> Actions{get;init;}=new(){{"Left","Back"},{"Right","Forward"},{"Vertical","Refresh"}};
    internal static readonly string[] Directions=["Left","Right","Vertical"];
    internal static readonly string[] Commands=["None","Back","Forward","Refresh"];
    internal static GestureOptions Load(string home)
    {
        try{var options=JsonSerializer.Deserialize<GestureOptions>(File.ReadAllText(Path.Combine(home,"gesture-options.json")))??new();if(options.Actions==null)return new();if(!options.Actions.ContainsKey("Vertical"))options.Actions["Vertical"]=options.Actions.GetValueOrDefault("UpDown",options.Actions.GetValueOrDefault("Up","Refresh"));return options with{Actions=Directions.ToDictionary(d=>d,d=>options.Actions.TryGetValue(d,out var action)&&Commands.Contains(action)?action:"None")};}catch{return new();}
    }
    internal void Save(string home){Directory.CreateDirectory(home);string file=Path.Combine(home,"gesture-options.json");File.WriteAllText(file+".tmp",JsonSerializer.Serialize(this));File.Move(file+".tmp",file,true);}
    internal string ActionFor(string direction)=>Enabled?Actions.GetValueOrDefault(direction,"None"):"None";
}
internal sealed partial class MainForm
{
    GestureOptions _gestureOptions=new();
    void InitializeGestureOptions(FlowLayoutPanel body)
    {

        var toggle=new CheckBox{Text="内蔵マウスジェスチャを使う",Checked=_gestureOptions.Enabled,AutoSize=true};body.Controls.Add(toggle);
        body.Controls.Add(new Label{Text="初期状態はOFFです。右ボタンを押しながら動かします。\nF5で更新、Alt＋左で戻る、Alt＋右で進みます。",AutoSize=true});
        var mappings=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Enabled=toggle.Checked};body.Controls.Add(mappings);
        bool restoring=false;
        toggle.CheckedChanged+=(_,_)=>{if(restoring)return;try{var next=_gestureOptions with{Enabled=toggle.Checked};next.Save(RecommendationHome);_gestureOptions=next;mappings.Enabled=toggle.Checked;}catch(Exception error){restoring=true;toggle.Checked=_gestureOptions.Enabled;restoring=false;MessageBox.Show(this,error.Message,"ジェスチャ設定を保存できません");}};
        string[] labels=["左 ←","右 →","上下 ↑↓"];string[] choices=["何もしない","戻る","進む","最新の情報に更新"];
        for(int i=0;i<GestureOptions.Directions.Length;i++)
        {
            string direction=GestureOptions.Directions[i];var row=new FlowLayoutPanel{AutoSize=true};row.Controls.Add(new Label{Text=labels[i],Width=100,Margin=new Padding(0,8,4,0)});var choice=new ComboBox{Tag="gesture-action",DropDownStyle=ComboBoxStyle.DropDownList,Width=220};choice.Items.AddRange(choices);choice.SelectedIndex=Array.IndexOf(GestureOptions.Commands,_gestureOptions.Actions.GetValueOrDefault(direction,"None"));row.Controls.Add(choice);mappings.Controls.Add(row);
            choice.SelectedIndexChanged+=(_,_)=>{if(restoring)return;try{var next=_gestureOptions with{Actions=new(_gestureOptions.Actions)};next.Actions[direction]=GestureOptions.Commands[choice.SelectedIndex];next.Save(RecommendationHome);_gestureOptions=next;}catch(Exception error){restoring=true;choice.SelectedIndex=Array.IndexOf(GestureOptions.Commands,_gestureOptions.Actions.GetValueOrDefault(direction,"None"));restoring=false;MessageBox.Show(this,error.Message,"ジェスチャ設定を保存できません");}};
        }
    }
}
