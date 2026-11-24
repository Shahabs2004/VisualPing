namespace VisualPing.Services;

public interface IUiDispatcher
{
    void Invoke(Action callback);
}
