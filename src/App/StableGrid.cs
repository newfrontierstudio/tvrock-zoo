namespace TsScheduler;

internal sealed class StableGrid:DataGridView
{
    internal StableGrid(){DoubleBuffered=true;SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
}
