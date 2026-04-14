using System.Runtime.InteropServices;

namespace Netor.Cortana.Pages;

/// <summary>
/// 暴露给 JavaScript 的浮动窗口桥接对象。
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class FloatBridgeHostObject
{
    private readonly FloatWindow _floatWindow;

    internal FloatBridgeHostObject(FloatWindow floatWindow)
    {
        _floatWindow = floatWindow;
    }

    /// <summary>
    /// 显示主窗口，供 JS 调用。
    /// </summary>
    public void ShowMainWindow()
    {
        _floatWindow.InvokeIfRequired(_floatWindow.ShowMainWindow);
    }
}
