using System.Text.Json;

namespace TsRecommend;

internal sealed class QuantumCognitionOptions
{
 public bool Enabled{get;set;}=true;public double Weight{get;set;}=.70;
// v2 parameters. Defaults reproduce the v1 constants where v1 had fixed values.
public double EtaScale{get;set;}=1;
public double HalfLifeDays{get;set;}=45;
public double PrecessionDays{get;set;}=21;
public double DecoherenceHalfLifeDays{get;set;}=60;
public double BusyRotationRadians{get;set;}=.30;
public double RivalryBoost{get;set;}=.15;
 internal static QuantumCognitionOptions Load(string home){string path=Path.Combine(home,"quantum-options.json");var options=File.Exists(path)?JsonSerializer.Deserialize<QuantumCognitionOptions>(File.ReadAllText(path))??new():new();if(!double.IsFinite(options.Weight)||options.Weight<0||options.Weight>1)throw new InvalidDataException("量子認知の反映率は0〜100%で指定してください。");
options.EtaScale=double.IsFinite(options.EtaScale)?Math.Clamp(options.EtaScale,.5,2):1;
options.HalfLifeDays=double.IsFinite(options.HalfLifeDays)?Math.Clamp(options.HalfLifeDays,7,180):45;
options.PrecessionDays=double.IsFinite(options.PrecessionDays)?Math.Clamp(options.PrecessionDays,3,180):21;
options.DecoherenceHalfLifeDays=double.IsFinite(options.DecoherenceHalfLifeDays)?Math.Clamp(options.DecoherenceHalfLifeDays,7,365):60;
options.BusyRotationRadians=double.IsFinite(options.BusyRotationRadians)?Math.Clamp(options.BusyRotationRadians,0,1):.30;
options.RivalryBoost=double.IsFinite(options.RivalryBoost)?Math.Clamp(options.RivalryBoost,0,.5):.15;return options;}
 internal void Save(string home){if(!double.IsFinite(Weight)||Weight<0||Weight>1)throw new InvalidDataException("量子認知の反映率を確認してください。");ZooStorage.DurableJson.Write(Path.Combine(home,"quantum-options.json"),JsonSerializer.Serialize(this));}
}
