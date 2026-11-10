namespace TsScheduler;
internal sealed class RecordingOffsetInput:FlowLayoutPanel
{
    internal ComboBox Direction{get;}=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=100,AccessibleName="調整する方向"};
    internal NumericUpDown Minutes{get;}=new(){Minimum=0,Maximum=60,Width=70,AccessibleName="調整する分"};
    internal NumericUpDown Seconds{get;}=new(){Minimum=0,Maximum=59,Width=64,AccessibleName="調整する秒"};
    bool updating;
    internal event EventHandler? ValueChanged;
    internal int Value {get=>(Direction.SelectedIndex==0?-1:1)*((int)Minutes.Value*60+(int)Seconds.Value);set{
        if(value is <-3600 or >3600)throw new ArgumentOutOfRangeException(nameof(value));updating=true;
        try{Direction.SelectedIndex=value<0?0:1;Minutes.Value=Math.Abs(value)/60;Seconds.Maximum=Minutes.Value==60?0:59;Seconds.Value=Math.Abs(value)%60;}finally{updating=false;}ValueChanged?.Invoke(this,EventArgs.Empty);
    }}
    internal string Summary=>$"{(Value<0?"−":Value>0?"＋":"")}{Minutes.Value:0}分{Seconds.Value:00}秒";
    internal RecordingOffsetInput(bool start,int seconds){
        AutoSize=true;WrapContents=false;Margin=new Padding(0,0,0,8);Direction.Items.AddRange(start?new object[]{"早める","遅らせる"}:new object[]{"短縮する","延長する"});
        Controls.AddRange([Direction,Minutes,new Label{Text="分",AutoSize=true,Margin=new Padding(2,6,4,0)},Seconds,new Label{Text="秒",AutoSize=true,Margin=new Padding(2,6,0,0)}]);
        Value=Math.Clamp(seconds,-3600,3600);if(seconds==0)Direction.SelectedIndex=start?0:1;
        Direction.SelectedIndexChanged+=Changed;Minutes.ValueChanged+=Changed;Seconds.ValueChanged+=Changed;
    }
    void Changed(object?sender,EventArgs e){if(updating)return;updating=true;try{Seconds.Maximum=Minutes.Value==60?0:59;}finally{updating=false;}ValueChanged?.Invoke(this,EventArgs.Empty);}
}

