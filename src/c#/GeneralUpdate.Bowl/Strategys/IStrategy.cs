using GeneralUpdate.Bowl.Internal;

namespace GeneralUpdate.Bowl.Strategys;

[System.Obsolete("Use Strategies.IBowlStrategy instead. Will be removed in v10.")]
internal interface IStrategy
{
    void Launch();
    
    void SetParameter(MonitorParameter parameter);
}