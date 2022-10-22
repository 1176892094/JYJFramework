# JYJFramework
欢迎来到JYJFramework介绍
1.导入
打开Unity的Package Manager左上角“+”号使用URL方式导入：https://github.com/1176892094/JYJFramework.git

2.使用

(1)EventManager

```csharp
public class Test : MonoBehaviour
{
    private void Awake()
    {
        EventManager.AddEventListener(EventName.EventTrigger, EventTrigger); //添加事件
    }

    private void Update()
    {
        EventManager.OnEventTrigger(EventName.EventTrigger); //触发事件
    }

    private void EventTrigger() //触发事件调用该方法
    {
        Debug.Log("触发事件!");
    }

    private void OnDestroy()
    {
        EventManager.RemoveEventListener(EventName.EventTrigger, EventTrigger); //移除事件
    }
}

public struct EventName
{
    public const string EventTrigger = "EventTrigger"; //建议定一个事件的常量
}
