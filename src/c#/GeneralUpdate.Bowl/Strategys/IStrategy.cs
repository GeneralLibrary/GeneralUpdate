using GeneralUpdate.Bowl.Internal;

namespace GeneralUpdate.Bowl.Strategys;

internal interface IStrategy
{
    void Launch();
    
    void SetParameter(MonitorParameter parameter);
}