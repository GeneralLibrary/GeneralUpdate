namespace GeneralUpdate.Bowl.Strategys;

public interface IStrategy
{
    void Launch();
    
    void SetParameter(MonitorParameter parameter);
}