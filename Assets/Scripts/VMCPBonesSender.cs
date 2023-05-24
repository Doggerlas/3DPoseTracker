/*将游戏物体的位置、旋转以及动画信息发送到其他应用程序或设备
 这个文件用于将游戏物体的位置、旋转以及动画信息发送到其他应用程序或设备。
 该脚本使用了uOSCClient库。它包含一个枚举类型VirtualDevice用来确定传输的设备类型，还包含一个动画控制器animator、一个设置旋转角度的函数SetRot以及一个发送骨骼位置信息的函数SendBoneTransformForTracker。
 最主要的函数是Update，它每帧负责发送所有需要传输的数据。
 */
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(uOscClientTDP))]
public class VMCPBonesSender : MonoBehaviour
{
    uOscClientTDP uClient = null;

    public GameObject Model = null;
    private GameObject OldModel = null;

    Animator animator = null;

    public enum VirtualDevice
    {
        HMD = 0,
        Controller = 1,
        Tracker = 2,
    }

    void Start()
    {
        uClient = GetComponent<uOscClientTDP>();
    }

    private float deg = 0f;
    private float flag = 1f;

    public void SetRot(bool f)
    {
        if(f)
        {
            deg = 180f;
            flag = -1f;
        }
        else
        {
            deg = 0;
            flag = 1f;
        }
    }

    void Update()
    {
        //モデルが更新されたときのみ読み込み
        if (Model != null && OldModel != Model)
        {
            animator = Model.GetComponent<Animator>();
            OldModel = Model;
        }

        if (Model != null && animator != null && uClient != null)
        {
            //Root
            var RootTransform = Model.transform;
            if (RootTransform != null)
            {
                uClient.Send("/VMC/Ext/Root/Pos",
                    "root",
                    flag * RootTransform.position.x, RootTransform.position.y, flag * RootTransform.position.z,
                    RootTransform.rotation.x, RootTransform.rotation.y + deg, RootTransform.rotation.z, RootTransform.rotation.w);
            }

            //Bones
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone != HumanBodyBones.LastBone)
                {
                    var Transform = animator.GetBoneTransform(bone);
                    if (Transform != null)
                    {
                        uClient.Send("/VMC/Ext/Bone/Pos",
                            bone.ToString(),
                            Transform.localPosition.x, Transform.localPosition.y, Transform.localPosition.z,
                            Transform.localRotation.x, Transform.localRotation.y, Transform.localRotation.z, Transform.localRotation.w);
                    }
                }
            }

            //ボーン位置を仮想トラッカーとして送信
            SendBoneTransformForTracker(HumanBodyBones.Head, "Head");
            SendBoneTransformForTracker(HumanBodyBones.Spine, "Spine");
            SendBoneTransformForTracker(HumanBodyBones.LeftHand, "LeftHand");
            SendBoneTransformForTracker(HumanBodyBones.RightHand, "RightHand");
            SendBoneTransformForTracker(HumanBodyBones.LeftFoot, "LeftFoot");
            SendBoneTransformForTracker(HumanBodyBones.RightFoot, "RightFoot");

            //Available
            uClient.Send("/VMC/Ext/OK", 1);
        }
        else
        {
            uClient.Send("/VMC/Ext/OK", 0);
        }
        uClient.Send("/VMC/Ext/T", Time.time);
    }

    void SendBoneTransformForTracker(HumanBodyBones bone, string DeviceSerial)
    {
        var DeviceTransform = animator.GetBoneTransform(bone);
        if (DeviceTransform != null)
        {
            uClient.Send("/VMC/Ext/Tra/Pos",
        (string)DeviceSerial,
        (float)DeviceTransform.position.x,
        (float)DeviceTransform.position.y,
        (float)DeviceTransform.position.z,
        (float)DeviceTransform.rotation.x,
        (float)DeviceTransform.rotation.y,
        (float)DeviceTransform.rotation.z,
        (float)DeviceTransform.rotation.w);
        }
    }
}