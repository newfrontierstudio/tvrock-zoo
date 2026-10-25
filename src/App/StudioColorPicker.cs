using System.Drawing.Drawing2D;
namespace TsScheduler;
// Independent HSV editor. No third-party UI source or assets.
internal sealed class StudioColorPicker:Form
{
    internal Color SelectedColor{get;private set;}
    internal event Action<Color>? ColorChanged;
    readonly Color original;double hue,saturation,value;bool syncing;
    readonly NumericUpDown[] numbers=new NumericUpDown[6];
    readonly TextBox hex=new(){Width=145,AccessibleName="HEX"};
    readonly Panel current=new(){Width=82,Height=48},previous=new(){Width=82,Height=48};
    readonly ColorSurface square,strip;
    internal StudioColorPicker(Color color)
    {
        original=color;Text="局の色";ClientSize=new Size(720,510);MinimumSize=new Size(736,549);StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;BackColor=Color.White;ShowInTaskbar=false;MaximizeBox=false;MinimizeBox=false;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=3,RowCount=2};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,42));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,230));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,64));Controls.Add(layout);
        square=new ColorSurface(false,()=> (hue,saturation,value),SetHsv){Dock=DockStyle.Fill,Margin=new Padding(0,0,10,0),AccessibleName="彩度と明るさ"};strip=new ColorSurface(true,()=> (hue,saturation,value),SetHsv){Dock=DockStyle.Fill,Margin=new Padding(0,0,14,0),AccessibleName="色相"};layout.Controls.Add(square,0,0);layout.Controls.Add(strip,1,0);
        var fields=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=false,Margin=Padding.Empty};layout.Controls.Add(fields,2,0);
        var swatches=new FlowLayoutPanel{AutoSize=true};var newer=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown};newer.Controls.Add(new Label{Text="新しい色",AutoSize=true});newer.Controls.Add(current);var older=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown};older.Controls.Add(new Label{Text="元の色",AutoSize=true});older.Controls.Add(previous);previous.BackColor=color;previous.Cursor=Cursors.Hand;previous.Click+=(_,_)=>SetColor(original);swatches.Controls.AddRange([newer,older]);fields.Controls.Add(swatches);
        string[] labels=["H","S","B","R","G","B"];
        for(int i=0;i<6;i++){int index=i;var row=new FlowLayoutPanel{AutoSize=true,Margin=new Padding(0,2,0,0)};var number=new NumericUpDown{Width=118,Maximum=i==0?360:i<3?100:255,DecimalPlaces=i<3?1:0,AccessibleName=new[]{"Hue","Saturation","Brightness","Red","Green","Blue"}[i]};numbers[i]=number;row.Controls.Add(new Label{Text=labels[i]+(i==0?" °":i<3?" %":""),Width=45,Height=25,TextAlign=ContentAlignment.MiddleLeft});row.Controls.Add(number);fields.Controls.Add(row);number.ValueChanged+=(_,_)=>{if(syncing)return;if(index<3)SetHsv((double)numbers[0].Value,(double)numbers[1].Value/100,(double)numbers[2].Value/100);else SetColor(Color.FromArgb((int)numbers[3].Value,(int)numbers[4].Value,(int)numbers[5].Value));};}
        var hexRow=new FlowLayoutPanel{AutoSize=true};hexRow.Controls.Add(new Label{Text="#",Width=20});hexRow.Controls.Add(hex);fields.Controls.Add(hexRow);hex.TextChanged+=(_,_)=>{if(syncing)return;if(TryHex(out var chosen)){hex.BackColor=Color.White;SetColor(chosen,false);}else hex.BackColor=Color.MistyRose;};
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,10,0,0)};layout.Controls.Add(buttons,0,1);layout.SetColumnSpan(buttons,3);var cancel=new Button{Text="キャンセル",AutoSize=true,DialogResult=DialogResult.Cancel};var ok=new Button{Text="適用",AutoSize=true};ok.Click+=(_,_)=>{if(!TryHex(out _)){hex.Focus();return;}DialogResult=DialogResult.OK;Close();};buttons.Controls.AddRange([cancel,ok]);AcceptButton=ok;CancelButton=cancel;SetColor(color);
    }
    bool TryHex(out Color color){string text=hex.Text.Trim().TrimStart('#');bool valid=text.Length==6&&int.TryParse(text,System.Globalization.NumberStyles.HexNumber,null,out _);color=valid?Color.FromArgb(255,Color.FromArgb(Convert.ToInt32(text,16))):SelectedColor;return valid;}
    internal void SetColor(Color color,bool updateHex=true){hue=color.GetHue();double max=Math.Max(color.R,Math.Max(color.G,color.B))/255d,min=Math.Min(color.R,Math.Min(color.G,color.B))/255d;saturation=max==0?0:(max-min)/max;value=max;Publish(Color.FromArgb(255,color),updateHex);}
    void SetHsv(double h,double s,double v){hue=h;saturation=s;value=v;Publish(FromHsv(h,s,v),true);}
    internal static Color FromHsv(double h,double s,double v){h=(h%360+360)%360;double c=v*s,x=c*(1-Math.Abs(h/60%2-1)),m=v-c;var rgb=h switch{<60=>(c,x,0d),<120=>(x,c,0d),<180=>(0d,c,x),<240=>(0d,x,c),<300=>(x,0d,c),_=>(c,0d,x)};int Byte(double n)=>(int)Math.Clamp(Math.Round((n+m)*255),0,255);return Color.FromArgb(Byte(rgb.Item1),Byte(rgb.Item2),Byte(rgb.Item3));}
    void Publish(Color color,bool updateHex){SelectedColor=color;syncing=true;try{decimal[] vals=[(decimal)hue,(decimal)saturation*100,(decimal)value*100,color.R,color.G,color.B];for(int i=0;i<6;i++)numbers[i].Value=Math.Clamp(vals[i],numbers[i].Minimum,numbers[i].Maximum);if(updateHex)hex.Text=$"{color.R:X2}{color.G:X2}{color.B:X2}";current.BackColor=color;square.Invalidate();strip.Invalidate();}finally{syncing=false;}ColorChanged?.Invoke(color);}
    sealed class ColorSurface(bool hueStrip,Func<(double h,double s,double v)> read,Action<double,double,double> change):Control
    {
        public ColorSurface():this(false,()=> (0,0,0),(_,_,_)=>{}){}
        protected override void OnHandleCreated(EventArgs e){SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint,true);base.OnHandleCreated(e);}
        protected override void OnPaint(PaintEventArgs e){var(h,s,v)=read();var bounds=ClientRectangle;if(bounds.Width<2||bounds.Height<2)return;if(hueStrip){using var gradient=new LinearGradientBrush(bounds,Color.Red,Color.Red,90);gradient.InterpolationColors=new ColorBlend{Colors=[Color.Red,Color.Yellow,Color.Lime,Color.Cyan,Color.Blue,Color.Magenta,Color.Red],Positions=[0,1/6f,2/6f,3/6f,4/6f,5/6f,1]};e.Graphics.FillRectangle(gradient,bounds);int y=(int)(h/360*(Height-1));e.Graphics.DrawRectangle(Pens.White,0,y-2,Width-1,4);e.Graphics.DrawLine(Pens.Black,0,y,Width,y);}else{using var color=new LinearGradientBrush(bounds,Color.White,FromHsv(h,1,1),0f);e.Graphics.FillRectangle(color,bounds);using var shade=new LinearGradientBrush(bounds,Color.Transparent,Color.Black,90);e.Graphics.FillRectangle(shade,bounds);int x=(int)(s*(Width-1)),y=(int)((1-v)*(Height-1));e.Graphics.DrawEllipse(Pens.Black,x-5,y-5,10,10);e.Graphics.DrawEllipse(Pens.White,x-4,y-4,8,8);}base.OnPaint(e);}
        void Pick(Point p){var(h,s,v)=read();if(hueStrip)h=360d*Math.Clamp(p.Y,0,Height-1)/Math.Max(1,Height-1);else{s=(double)Math.Clamp(p.X,0,Width-1)/Math.Max(1,Width-1);v=1-(double)Math.Clamp(p.Y,0,Height-1)/Math.Max(1,Height-1);}change(h,s,v);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Capture=true;Pick(e.Location);}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture&&e.Button==MouseButtons.Left)Pick(e.Location);}
        protected override void OnMouseUp(MouseEventArgs e){if(Capture){Pick(e.Location);Capture=false;}base.OnMouseUp(e);}
        protected override void OnResize(EventArgs e){base.OnResize(e);Invalidate();}
    }
}
