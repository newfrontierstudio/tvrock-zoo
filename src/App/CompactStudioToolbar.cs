namespace TsScheduler;
internal sealed partial class MainForm
{
    void CompactStudioToolbar(FlowLayoutPanel toolbar)
    {
        toolbar.SuspendLayout();if(_tabs.TabPages.Count>0)_tabs.TabPages[0].Text="HOME";
        try{
            toolbar.MinimumSize=Size.Empty;toolbar.Padding=new Padding(4);toolbar.Margin=Padding.Empty;
            _latestInformationState.Visible=false;
            void UpdateHint()=>_recommendTip.SetToolTip(_latestInformation,_latestInformationState.Text);
            _latestInformationState.TextChanged+=(_,_)=>UpdateHint();UpdateHint();
            foreach(Control control in toolbar.Controls)toolbar.SetFlowBreak(control,false);
            foreach(var button in toolbar.Controls.OfType<Button>().Where(b=>b.AccessibleName?.Contains("文字")==true||b==_undoButton)){
                button.Text=button==_undoButton?"UNDO":button.AccessibleName?.Contains("小さく")==true?"文字-":"文字+";button.TextAlign=ContentAlignment.MiddleCenter;button.UseCompatibleTextRendering=false;button.Font=new Font(Font.FontFamily,7.225f);button.MinimumSize=Size.Empty;button.Padding=Padding.Empty;button.Margin=new Padding(3);button.AutoSize=false;var measured=TextRenderer.MeasureText(button.Text,button.Font,Size.Empty,TextFormatFlags.SingleLine);int inset=(int)Math.Ceiling(16*button.DeviceDpi/96f);button.Size=new Size(measured.Width+inset,measured.Height+inset);
            }
            foreach(Control action in new Control[]{_recommend,_recommendToggle,_latestInformation}){action.Font=new Font(Font.FontFamily,Font.SizeInPoints*.825f);action.MinimumSize=Size.Empty;action.Padding=Padding.Empty;action.Margin=new Padding(3);action.AutoSize=false;var measured=TextRenderer.MeasureText(action.Text,action.Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);int inset=(int)Math.Ceiling(15.4*action.DeviceDpi/96f);action.Size=new Size(measured.Width+inset,measured.Height+inset);}var compact=toolbar.Controls.OfType<Button>().Where(b=>b.AccessibleName?.Contains("文字")==true).ToArray();
            int position=toolbar.Controls.GetChildIndex(_latestInformation)+1;
            foreach(var button in compact)toolbar.Controls.SetChildIndex(button,position++);
            toolbar.Controls.SetChildIndex(_undoButton,position++);
            toolbar.Controls.SetChildIndex(_engineStatus,toolbar.Controls.Count-1);var buttons=toolbar.Controls.Cast<Control>().Where(c=>c.Visible&&(c is Button||c==_recommendToggle)).ToArray();int maxHeight=buttons.Select(c=>c.Height).DefaultIfEmpty(32).Max();foreach(var control in buttons){int top=3+(maxHeight-control.Height)/2;control.Margin=new Padding(3,top,3,3+maxHeight-control.Height-(top-3));}
            _engineStatus.Height=32;_engineStatus.Padding=new Padding(8,7,0,0);_engineStatus.Margin=new Padding(3,3,0,0);
        }finally{toolbar.ResumeLayout(true);}
        // Trigger the existing width calculation after controls reach their final sizes.
        toolbar.PerformLayout();var width=toolbar.ClientSize.Width;
        _engineStatus.Width=Math.Max(150,width-toolbar.Padding.Horizontal-toolbar.Controls.Cast<Control>().Where(c=>c.Visible&&c!=_engineStatus).Sum(c=>c.Width+c.Margin.Horizontal)-_engineStatus.Margin.Horizontal-12);
    }
}
