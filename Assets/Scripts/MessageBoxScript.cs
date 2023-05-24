/*
 * 
实现消息框的功能，如显示、隐藏、设置消息文本和回调函数等
此程序文件名是 MessageBoxScript.cs，它是用 C# 语言编写的一个 Unity3D 游戏开发脚本。主要用于实现游戏中的消息框功能，包含了隐藏、显示、设置消息文本和回调函数等方法。其中，回调函数是由布尔类型参数决定是否执行的委托类型。该程序包含了以下成员变量和方法：

成员变量：

private Text Message;：表示消息框中显示的文本
private Button BtnOk;：表示确认按钮
private Button BtnCancel;：表示取消按钮
private Action callback;：表示回调函数委托
构造方法：

无
成员函数：

void Init()：初始化成员变量
void Hide()：隐藏游戏对象
void ShowMessage(string msg)：显示文本和确认按钮，隐藏取消按钮，并将回调函数置为 null
void ShowMessage(string msg, Action messageBoxCallback)：显示文本、确认按钮和取消按钮，设置回调函数委托
void onMessageOK()：隐藏游戏对象，并调用回调函数委托，参数为 true
void onMessageCancel()：隐藏游戏对象，并调用回调函数委托，参数为 false
 */
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MessageBoxScript : MonoBehaviour
{
    private Text Message;
    private Button BtnOk;
    private Button BtnCancel;

    public delegate void MessageBoxCallback(bool ok);
    private Action<bool> callback;

    public void Init()
    {
        Message = GameObject.Find("Message").GetComponent<Text>();
        BtnOk = GameObject.Find("btnMessageOK").GetComponent<Button>();
        BtnCancel = GameObject.Find("btnMessageCancel").GetComponent<Button>();
    }

    public void Hide()
    {
        this.gameObject.SetActive(false);
    }

    public void ShowMessage(string msg)
    {
        Message.text = msg;
        BtnOk.gameObject.SetActive(true);
        BtnOk.transform.localPosition = new Vector3(0, -41, 0);
        BtnCancel.gameObject.SetActive(false);
        this.gameObject.SetActive(true);
        callback = null;
    }

    public void ShowMessage(string msg, Action<bool> messageBoxCallback)
    {
        Message.text = msg;
        BtnOk.gameObject.SetActive(true);
        BtnOk.transform.localPosition = new Vector3(-90, -41, 0);
        BtnCancel.gameObject.SetActive(true);
        this.gameObject.SetActive(true);
        callback = null;
        callback = messageBoxCallback;
    }

    public void onMessageOK()
    {
        this.gameObject.SetActive(false);
        callback?.Invoke(true);
    }

    public void onMessageCancel()
    {
        this.gameObject.SetActive(false);
        callback?.Invoke(false);
    }
}
