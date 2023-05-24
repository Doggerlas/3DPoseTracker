/*模型文件，提供姿势捕捉模型的代码实现*/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 关节名称和整数值索引:如果索引号未指定，枚举常量的默认关联整数值将会是前一个常量的关联整数值加1
public enum PositionIndex : int
{
    rShldrBend = 0, //0
    rForearmBend,   //1
    rHand,          //2
    rThumb2,        //3
    rMid1,          //4

    lShldrBend,     //5
    lForearmBend,   //6
    lHand,          //7
    lThumb2,        //8
    lMid1,          //9

    lEar,           //10
    lEye,           //11
    rEar,           //12
    rEye,           //13
    Nose,           //14

    rThighBend,     //15
    rShin,          //16
    rFoot,          //17
    rToe,           //18

    lThighBend,     //19
    lShin,          //20
    lFoot,          //21
    lToe,           //22

    abdomenUpper,   //23

    //Calculated coordinates
    hip,            //24
    head,           //25
    neck,           //26
    spine,          //27

    Count,          //28
    None,           //29
}
// 将枚举类型PositionIndex转换为整数类型。
public static partial class EnumExtend
{
    public static int Int(this PositionIndex i)
    {
        return (int)i;
    }
}

public class VNectModel : MonoBehaviour
{
    //关节点属性
    public class JointPoint
    {
        public PositionIndex Index;                 //关节点名称索引
        public Vector2 Pos2D = new Vector2();       //关节点在二维空间中的位置
        public float Score2D;                       //关节点在二维空间中的得分

        public Vector3 Pos3D = new Vector3();       //关节点在三维空间中的位置
        public Vector3 Now3D = new Vector3();       //关节点当前在三维空间中的位置
        //public Vector3 PPos3D = new Vector3();    
        //public Vector3 PPPos3D = new Vector3();
        public Vector3[] PrevPos3D = new Vector3[10];//表示关节点在过去10个时间步内的历史位置
        //public Vector3 PrevNow3D = new Vector3();
        //public Vector3 PPrevNow3D = new Vector3();
        //public Vector3 Predicted3D = new Vector3();
        //public Vector3 VecNow3D = new Vector3();
        //public Vector3 PPredicted3D = new Vector3();
        //public Vector3 AccNow3D = new Vector3();
        //public Vector3 VelNow3D = new Vector3();
        //public float VecAngle;
        public float Score3D;                       //关节点在三维空间中的得分
        public bool Visibled;                       //关节点是否可见
        public int Error;                           //关节点的误差
        //public float VecNow3DMagnitude;
        //public bool RattlingCheck;
        //public float RattlingCheckFrame;
        //public float Threshold;
        //public float Smooth;
        //public float Ratio;
        public bool UpperBody;                       //关节点是否属于上半身
        public bool Lock;                            //关节点是否被锁定
        public int maxXIndex;                        //关节点在三维空间中的最大索引值(XYZ轴)
        public int maxYIndex;
        public int maxZIndex;

        // Bones
        public Transform Transform = null;      //关节点的Transform信息
        public Quaternion InitRotation;         //关节点的初始自旋角度
        public Quaternion Inverse;              //表示forward到(关节点-子关节点)向量的旋转四元数
        public Quaternion InverseRotation;      //以上二者相乘 代表从forward到关节点当前朝向的总旋转

        public JointPoint Child = null;         //关节点的子节点
        public JointPoint Parent = null;        //关节点的父节点
        public float VecFlag = 1f;              //示关节点的向量标志

        public static float Q = 0.001f;         //QR 表示用于卡尔曼滤波的参数
        public static float R = 0.0015f;
        public Vector3 P = new Vector3();       //卡尔曼滤波的过程误差协方差矩阵
        public Vector3 X = new Vector3();       //卡尔曼滤波的状态向量
        public Vector3 K = new Vector3();       //卡尔曼滤波的卡尔曼增益向量
    }
    //骨架属性
    public class Skeleton
    {
        public GameObject LineObject;           //骨架的线对象
        public LineRenderer Line;               //骨架的线渲染器

        public JointPoint start = null;         //骨架的起始关节点
        public JointPoint end = null;           //表示骨架的结束关节点
        public bool upperBody = false;          //表示骨架是否属于上半身
    }

    private List<Skeleton> Skeletons = new List<Skeleton>();    //完整的骨架
    public Material SkeletonMaterial;                           //骨架材质
    //表示骨架的位置和缩放
    public float SkeletonX;
    public float SkeletonY;
    public float SkeletonZ;
    public float SkeletonScale;

    // Joint position and bone
    private JointPoint[] jointPoints;       //关节点数组
    public JointPoint[] JointPoints { get { return jointPoints; } }//获取关节点数组

    private Vector3 initPosition;           //初始中心位置

    private Quaternion InitGazeRotation;    //初始注视旋转
    private Quaternion gazeInverse;         //注视的逆旋转

    // UnityChan
    public GameObject ModelObject;              //Unity模型对象
    public GameObject Nose;                     //鼻子的对象
    private Animator anim;                      //动画控制器

    private float movementScale = 0.01f;        //移动的比例尺
    private float centerTall = 224 * 0.75f;     //模型中心高度
    private float tall = 224 * 0.75f;           //模型高度
    private float prevTall = 224 * 0.75f;       //模型先前的高度
    public float ZScale = 0.8f;                 //模型沿Z轴的缩放比例

    //表示是否锁定脚、腿和手的标志
    private bool LockFoot = false;
    private bool LockLegs = false;
    private bool LockHand = false;
    //脚和脚趾的IK位置。
    private float FootIKY = 0f;
    private float ToeIKY = 0f;
    //是否处于上半身模式
    private bool UpperBodyMode = false;
    //上半身的比例 0f或者1f
    private float UpperBodyF = 1f;

    /**** Foot IK ****/
    [SerializeField]
    private bool useIK = true;
    //　是否在 IK 中启用角度
    [SerializeField]
    private bool useIKRot = true;
    //　右腿权重
    private float rightFootWeight = 0f;
    //　左腿权重
    private float leftFootWeight = 0f;
    //　右脚位置
    private Vector3 rightFootPos;
    //　左脚位置
    private Vector3 leftFootPos;
    //　右腿角度
    private Quaternion rightFootRot;
    //　左腿角度
    private Quaternion leftFootRot;
    //　右脚和左脚之间的距离
    private float distance;
    //　脚接触位置的偏移值
    [SerializeField]
    private float offset = 0.1f;
    //　对撞机中心位置
    private Vector3 defaultCenter;
    //　投射距离
    [SerializeField]
    private float rayRange = 1f;

    //　调整对撞机位置时的速度
    [SerializeField]
    private float smoothing = 100f;

    //　射线飞行位置调整值
    [SerializeField]
    private Vector3 rayPositionOffset = Vector3.up * 0.3f;

    //初始化 由VNectBarracudaRunner调用
    public JointPoint[] Init(int inputImageSize, ConfigurationSetting config)
    {
        //令人物模型动态适应传进来的视频尺寸
        movementScale = 0.01f * 224f / inputImageSize;
        centerTall = inputImageSize * 0.75f;
        tall = inputImageSize * 0.75f;
        prevTall = inputImageSize * 0.75f;

        // 生成28个关节点 赋予部分关节点属性
        jointPoints = new JointPoint[PositionIndex.Count.Int()];
        for (var i = 0; i < PositionIndex.Count.Int(); i++)
        {
            jointPoints[i] = new JointPoint();
            jointPoints[i].Index = (PositionIndex)i;
            jointPoints[i].Score3D = 1;
            //jointPoints[i].RattlingCheck = false;
            //jointPoints[i].VecNow3DMagnitude = 0;
            jointPoints[i].UpperBody = false;
            jointPoints[i].Lock = false;
            jointPoints[i].Error = 0;
        }
        /****************************************************获取模型的动画控制器 并初始化hip的位置信息***********************************************************/
        anim = ModelObject.GetComponent<Animator>();
        jointPoints[PositionIndex.hip.Int()].Transform = transform;//hip位置即为模型的中心
        //检测关节点与动画控制器的关节点绑定
        //右臂
        jointPoints[PositionIndex.rShldrBend.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        jointPoints[PositionIndex.rForearmBend.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        jointPoints[PositionIndex.rHand.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.RightHand);
        jointPoints[PositionIndex.rThumb2.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.RightThumbIntermediate);
        jointPoints[PositionIndex.rMid1.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        //左臂
        jointPoints[PositionIndex.lShldrBend.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        jointPoints[PositionIndex.lForearmBend.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        jointPoints[PositionIndex.lHand.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        jointPoints[PositionIndex.lThumb2.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.LeftThumbIntermediate);
        jointPoints[PositionIndex.lMid1.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
        // 脸
        jointPoints[PositionIndex.lEar.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.Head);
        jointPoints[PositionIndex.lEye.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.LeftEye);
        jointPoints[PositionIndex.rEar.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.Head);
        jointPoints[PositionIndex.rEye.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.RightEye);
        jointPoints[PositionIndex.Nose.Int()].Transform = Nose.transform;

        //右腿
        jointPoints[PositionIndex.rThighBend.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        jointPoints[PositionIndex.rShin.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        jointPoints[PositionIndex.rFoot.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        jointPoints[PositionIndex.rToe.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.RightToes);

        //左腿
        jointPoints[PositionIndex.lThighBend.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        jointPoints[PositionIndex.lShin.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        jointPoints[PositionIndex.lFoot.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        jointPoints[PositionIndex.lToe.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.LeftToes);

        // etc
        jointPoints[PositionIndex.abdomenUpper.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.Spine);
        jointPoints[PositionIndex.head.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.Head);
        jointPoints[PositionIndex.hip.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.Hips);
        jointPoints[PositionIndex.neck.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.Neck);
        jointPoints[PositionIndex.spine.Int()].Transform = anim.GetBoneTransform(HumanBodyBones.Spine);

        // 上半身关节点选定
        jointPoints[PositionIndex.hip.Int()].UpperBody = true;
        // 右臂
        jointPoints[PositionIndex.rShldrBend.Int()].UpperBody = true;
        jointPoints[PositionIndex.rForearmBend.Int()].UpperBody = true;
        jointPoints[PositionIndex.rHand.Int()].UpperBody = true;
        jointPoints[PositionIndex.rThumb2.Int()].UpperBody = true;
        jointPoints[PositionIndex.rMid1.Int()].UpperBody = true;
        // 左臂
        jointPoints[PositionIndex.lShldrBend.Int()].UpperBody = true;
        jointPoints[PositionIndex.lForearmBend.Int()].UpperBody = true;
        jointPoints[PositionIndex.lHand.Int()].UpperBody = true;
        jointPoints[PositionIndex.lThumb2.Int()].UpperBody = true;
        jointPoints[PositionIndex.lMid1.Int()].UpperBody = true;
        // 脸
        jointPoints[PositionIndex.lEar.Int()].UpperBody = true;
        jointPoints[PositionIndex.lEye.Int()].UpperBody = true;
        jointPoints[PositionIndex.rEar.Int()].UpperBody = true;
        jointPoints[PositionIndex.rEye.Int()].UpperBody = true;
        jointPoints[PositionIndex.Nose.Int()].UpperBody = true;
        // etc
        jointPoints[PositionIndex.spine.Int()].UpperBody = true;
        jointPoints[PositionIndex.neck.Int()].UpperBody = true;

        /*************************************************************关节点父子节点关系设置*************************************************************************/
        // 右臂：右肩的子节点是右肘 右肘的子节点是右手
        jointPoints[PositionIndex.rShldrBend.Int()].Child = jointPoints[PositionIndex.rForearmBend.Int()];
        jointPoints[PositionIndex.rForearmBend.Int()].Child = jointPoints[PositionIndex.rHand.Int()];
        if (config.ElbowAxisTop == 0)
        {
            jointPoints[PositionIndex.rForearmBend.Int()].Parent = jointPoints[PositionIndex.rShldrBend.Int()];
        }
        //jointPoints[PositionIndex.rHand.Int()].Parent = jointPoints[PositionIndex.rForearmBend.Int()];

        // 左臂
        jointPoints[PositionIndex.lShldrBend.Int()].Child = jointPoints[PositionIndex.lForearmBend.Int()];
        jointPoints[PositionIndex.lForearmBend.Int()].Child = jointPoints[PositionIndex.lHand.Int()];
        if (config.ElbowAxisTop == 0)
        {
            jointPoints[PositionIndex.lForearmBend.Int()].Parent = jointPoints[PositionIndex.lShldrBend.Int()];
        }
        //jointPoints[PositionIndex.lHand.Int()].Parent = jointPoints[PositionIndex.lForearmBend.Int()];

        // Fase

        //右腿
        jointPoints[PositionIndex.rThighBend.Int()].Child = jointPoints[PositionIndex.rShin.Int()];
        jointPoints[PositionIndex.rShin.Int()].Child = jointPoints[PositionIndex.rFoot.Int()];
        jointPoints[PositionIndex.rFoot.Int()].Child = jointPoints[PositionIndex.rToe.Int()];
        jointPoints[PositionIndex.rFoot.Int()].Parent = jointPoints[PositionIndex.rShin.Int()];
         
        // 左腿
        jointPoints[PositionIndex.lThighBend.Int()].Child = jointPoints[PositionIndex.lShin.Int()];
        jointPoints[PositionIndex.lShin.Int()].Child = jointPoints[PositionIndex.lFoot.Int()];
        jointPoints[PositionIndex.lFoot.Int()].Child = jointPoints[PositionIndex.lToe.Int()];
        jointPoints[PositionIndex.lFoot.Int()].Parent = jointPoints[PositionIndex.lShin.Int()];

        // etc
        jointPoints[PositionIndex.spine.Int()].Child = jointPoints[PositionIndex.neck.Int()];
        jointPoints[PositionIndex.neck.Int()].Child = jointPoints[PositionIndex.head.Int()];
        //jointPoints[PositionIndex.head.Int()].Child = jointPoints[PositionIndex.Nose.Int()];
        //jointPoints[PositionIndex.hip.Int()].Child = jointPoints[PositionIndex.spine.Int()];

        /*******************************************相邻关节点之间新建一根骨头 添加到整体的骨架列表**************************************************************/
        // 右臂
        AddSkeleton(PositionIndex.rShldrBend, PositionIndex.rForearmBend, true);
        AddSkeleton(PositionIndex.rForearmBend, PositionIndex.rHand, true);
        AddSkeleton(PositionIndex.rHand, PositionIndex.rThumb2, true);
        AddSkeleton(PositionIndex.rHand, PositionIndex.rMid1, true);

        // 左臂
        AddSkeleton(PositionIndex.lShldrBend, PositionIndex.lForearmBend, true);
        AddSkeleton(PositionIndex.lForearmBend, PositionIndex.lHand, true);
        AddSkeleton(PositionIndex.lHand, PositionIndex.lThumb2, true);
        AddSkeleton(PositionIndex.lHand, PositionIndex.lMid1, true);

        // 脸
        //AddSkeleton(PositionIndex.lEar, PositionIndex.lEye);
        //AddSkeleton(PositionIndex.lEye, PositionIndex.Nose);
        //AddSkeleton(PositionIndex.rEar, PositionIndex.rEye);
        //AddSkeleton(PositionIndex.rEye, PositionIndex.Nose);
        AddSkeleton(PositionIndex.lEar, PositionIndex.Nose, true);
        AddSkeleton(PositionIndex.rEar, PositionIndex.Nose, true);

        // 右腿
        AddSkeleton(PositionIndex.rThighBend, PositionIndex.rShin, false);
        AddSkeleton(PositionIndex.rShin, PositionIndex.rFoot, false);
        AddSkeleton(PositionIndex.rFoot, PositionIndex.rToe, false);

        // 左腿
        AddSkeleton(PositionIndex.lThighBend, PositionIndex.lShin, false);
        AddSkeleton(PositionIndex.lShin, PositionIndex.lFoot, false);
        AddSkeleton(PositionIndex.lFoot, PositionIndex.lToe, false);

        // etc
        AddSkeleton(PositionIndex.spine, PositionIndex.neck, true);
        AddSkeleton(PositionIndex.neck, PositionIndex.head, true);
        AddSkeleton(PositionIndex.head, PositionIndex.Nose, true);
        AddSkeleton(PositionIndex.neck, PositionIndex.rShldrBend, true);
        AddSkeleton(PositionIndex.neck, PositionIndex.lShldrBend, true);
        AddSkeleton(PositionIndex.rThighBend, PositionIndex.rShldrBend, true);
        AddSkeleton(PositionIndex.lThighBend, PositionIndex.lShldrBend, true);
        AddSkeleton(PositionIndex.rShldrBend, PositionIndex.abdomenUpper, true);
        AddSkeleton(PositionIndex.lShldrBend, PositionIndex.abdomenUpper, true);
        AddSkeleton(PositionIndex.rThighBend, PositionIndex.abdomenUpper, true);
        AddSkeleton(PositionIndex.lThighBend, PositionIndex.abdomenUpper, true);
        AddSkeleton(PositionIndex.lThighBend, PositionIndex.rThighBend, true);

        /******************************************初始化关节点的旋转信息，计算并设置骨骼的初始位置和反向旋转四元数*******************************************/
        //计算hip lThighBend rThighBend法向 定义为模型正向forward向量
        var forward = TriangleNormal(jointPoints[PositionIndex.hip.Int()].Transform.position, jointPoints[PositionIndex.lThighBend.Int()].Transform.position, jointPoints[PositionIndex.rThighBend.Int()].Transform.position);
        foreach (var jointPoint in jointPoints)
        {
            if (jointPoint.Transform != null)
            {
                jointPoint.InitRotation = jointPoint.Transform.rotation;//记录关节点的自旋信息
            }

            if (jointPoint.Child != null)
            {
                jointPoint.Inverse = GetInverse(jointPoint, jointPoint.Child, forward);//记录forward向量旋转到(关节点-关节点子节点)向量的旋转四元数
                jointPoint.InverseRotation = jointPoint.Inverse * jointPoint.InitRotation;//InverseRotation总旋转
            }
        }
        //记录hip的旋转信息InverseRotation：Z轴到forward向量旋转的四元数
        var hip = jointPoints[PositionIndex.hip.Int()];
        initPosition = transform.position;
        //initPosition = jointPoints[PositionIndex.hip.Int()].Transform.position;
        hip.Inverse = Quaternion.Inverse(Quaternion.LookRotation(forward));
        hip.InverseRotation = hip.Inverse * hip.InitRotation;

        // 头部旋转信息InverseRotation 居然用鼻子计算
        var head = jointPoints[PositionIndex.head.Int()];
        head.InitRotation = jointPoints[PositionIndex.head.Int()].Transform.rotation;
        var gaze = jointPoints[PositionIndex.Nose.Int()].Transform.position - jointPoints[PositionIndex.head.Int()].Transform.position;
        head.Inverse = Quaternion.Inverse(Quaternion.LookRotation(gaze));
        head.InverseRotation = head.Inverse * head.InitRotation;

        // 左手旋转信息InverseRotation 用左中指和大拇指计算 
        var lHand = jointPoints[PositionIndex.lHand.Int()];
        var lf = TriangleNormal(lHand.Pos3D, jointPoints[PositionIndex.lMid1.Int()].Pos3D, jointPoints[PositionIndex.lThumb2.Int()].Pos3D);
        lHand.InitRotation = lHand.Transform.rotation;
        lHand.Inverse = Quaternion.Inverse(Quaternion.LookRotation(jointPoints[PositionIndex.lThumb2.Int()].Transform.position - jointPoints[PositionIndex.lMid1.Int()].Transform.position, lf));
        lHand.InverseRotation = lHand.Inverse * lHand.InitRotation;

        // 右手旋转信息InverseRotation 用右中指和大拇指计算 
        var rHand = jointPoints[PositionIndex.rHand.Int()];
        var rf = TriangleNormal(rHand.Pos3D, jointPoints[PositionIndex.rThumb2.Int()].Pos3D, jointPoints[PositionIndex.rMid1.Int()].Pos3D);
        rHand.InitRotation = jointPoints[PositionIndex.rHand.Int()].Transform.rotation;
        rHand.Inverse = Quaternion.Inverse(Quaternion.LookRotation(jointPoints[PositionIndex.rThumb2.Int()].Transform.position - jointPoints[PositionIndex.rMid1.Int()].Transform.position, rf));
        rHand.InverseRotation = rHand.Inverse * rHand.InitRotation;

        //关节点初始3D得分
        jointPoints[PositionIndex.hip.Int()].Score3D = 1f;
        jointPoints[PositionIndex.neck.Int()].Score3D = 1f;
        jointPoints[PositionIndex.Nose.Int()].Score3D = 1f;
        jointPoints[PositionIndex.head.Int()].Score3D = 1f;
        jointPoints[PositionIndex.spine.Int()].Score3D = 1f;
        /*
        jointPoints[PositionIndex.rForearmBend.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.rHand.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.rThumb2.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.rMid1.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.lForearmBend.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.lHand.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.lThumb2.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.lMid1.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.rShin.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.rFoot.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.rToe.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.lShin.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.lFoot.Int()].RattlingCheck = true;
        jointPoints[PositionIndex.lToe.Int()].RattlingCheck = true;
        */
        SetPredictSetting(config);//读取一些关节点是否被lock的设置以及是否给rForearmBend和lForearmBend设定父亲

        //计算对撞机中心位置（defaultCenter）、脚部 IK 的 Y 偏移量（FootIKY）和脚趾部分的 IK 的 Y 偏移量（ToeIKY）。
        defaultCenter = new Vector3(transform.position.x, (jointPoints[PositionIndex.rToe.Int()].Transform.position.y + jointPoints[PositionIndex.lToe.Int()].Transform.position.y) /2f, transform.position.z);
        FootIKY = (jointPoints[PositionIndex.rFoot.Int()].Transform.position.y + jointPoints[PositionIndex.lFoot.Int()].Transform.position.y) / 2f + 0.1f;
        ToeIKY = (jointPoints[PositionIndex.rToe.Int()].Transform.position.y + jointPoints[PositionIndex.lToe.Int()].Transform.position.y) / 2f;

        return JointPoints;
    }

    //通过提供的 AvatarSetting 对象来设置角色的名称、位置、鼻子位置、缩放、骨骼可见性和骨骼位置缩放等
    public void SetSettings(AvatarSetting setting)
    {
        this.name = setting.AvatarName;

        ResetPosition(setting.PosX, setting.PosY, setting.PosZ);
        SetNose(setting.FaceOriX, setting.FaceOriY, setting.FaceOriZ);
        SetScale(setting.Scale);
        SetZScale(setting.DepthScale);

        SetSkeleton(setting.SkeletonVisible == 1);

        SkeletonX = setting.SkeletonPosX;
        SkeletonY = setting.SkeletonPosY;
        SkeletonZ = setting.SkeletonPosZ;
        SkeletonScale = setting.SkeletonScale;
    }

    //通过提供的坐标 (x, y, z) 来重置角色的位置
    public void ResetPosition(float x, float y, float z)
    {
        transform.position = new Vector3(x, y, z);
        initPosition = transform.position;
    }

    //设置鼻子的位置.方法是根据头部骨骼的位置和给定的偏移量计算新的鼻子位置，并将其应用于鼻子对象的变换组件
    public void SetNose(float x, float y, float z)
    {
        if (this.Nose == null)
        {
            this.Nose = new GameObject(this.name + "_Nose");
        }
        var ani = ModelObject.GetComponent<Animator>();
        var t = ani.GetBoneTransform(HumanBodyBones.Head);
        this.Nose.transform.position = new Vector3(t.position.x + x, t.position.y + y, t.position.z + z);

    }

    //通过提供的scale来重置角色的缩放
    public void SetScale(float scale)
    {
        transform.localScale = new Vector3(scale, scale, scale);
    }

    //通过提供的zscale来重置角色沿Z轴的缩放
    public void SetZScale(float zScale)
    {
        ZScale = zScale;
    }

    //可视化每根骨头 如果flag为false为不可见
    public void SetSkeleton(bool flag)
    {
        foreach (var sk in Skeletons)
        {
            sk.LineObject.SetActive(flag);
        }
    }

    //不可见骨头
    public void Hide()
    {
        SetSkeleton(false);
    }

    //可见骨头
    public void Show()
    {
    }

    //获取头部位置
    public Vector3 GetHeadPosition()
    {
        return anim.GetBoneTransform(HumanBodyBones.Head).position;
    }

    //是否为上半身模式
    public void SetUpperBodyMode(bool upper)
    {
        UpperBodyMode = upper;
        UpperBodyF = upper ? 0f : 1f;
    }

    //读取并设置表Predict的配置信息
    public void SetPredictSetting(ConfigurationSetting config)
    {
        if(jointPoints == null)
        {
            return;
        }
        /*
        for (var i = 0; i < PositionIndex.Count.Int(); i++)
        {
            jointPoints[i].RattlingCheckFrame = 5;
            jointPoints[i].VecNow3DMagnitude = 0;
            jointPoints[i].Threshold = config.OtherThreshold;
            jointPoints[i].Smooth = config.OtherSmooth;
            jointPoints[i].Ratio = config.OtherRatio;
        }
   
        jointPoints[PositionIndex.lShldrBend.Int()].RattlingCheckFrame = config.ShoulderRattlingCheckFrame;
        jointPoints[PositionIndex.rShldrBend.Int()].RattlingCheckFrame = config.ShoulderRattlingCheckFrame;
        jointPoints[PositionIndex.lThighBend.Int()].RattlingCheckFrame = config.ThighRattlingCheckFrame;
        jointPoints[PositionIndex.rThighBend.Int()].RattlingCheckFrame = config.ThighRattlingCheckFrame;
        jointPoints[PositionIndex.lShin.Int()].RattlingCheckFrame = config.FootRattlingCheckFrame;
        jointPoints[PositionIndex.rShin.Int()].RattlingCheckFrame = config.FootRattlingCheckFrame;
        jointPoints[PositionIndex.lFoot.Int()].RattlingCheckFrame = config.FootRattlingCheckFrame;
        jointPoints[PositionIndex.rFoot.Int()].RattlingCheckFrame = config.FootRattlingCheckFrame;
        jointPoints[PositionIndex.lToe.Int()].RattlingCheckFrame = config.FootRattlingCheckFrame;
        jointPoints[PositionIndex.rToe.Int()].RattlingCheckFrame = config.FootRattlingCheckFrame;
        jointPoints[PositionIndex.lForearmBend.Int()].RattlingCheckFrame = config.ArmRattlingCheckFrame;
        jointPoints[PositionIndex.lHand.Int()].RattlingCheckFrame = config.ArmRattlingCheckFrame;
        jointPoints[PositionIndex.lThumb2.Int()].RattlingCheckFrame = config.ArmRattlingCheckFrame;
        jointPoints[PositionIndex.lMid1.Int()].RattlingCheckFrame = config.ArmRattlingCheckFrame;
        jointPoints[PositionIndex.rForearmBend.Int()].RattlingCheckFrame = config.ArmRattlingCheckFrame;
        jointPoints[PositionIndex.rHand.Int()].RattlingCheckFrame = config.ArmRattlingCheckFrame;
        jointPoints[PositionIndex.rThumb2.Int()].RattlingCheckFrame = config.ArmRattlingCheckFrame;
        jointPoints[PositionIndex.rMid1.Int()].RattlingCheckFrame = config.ArmRattlingCheckFrame;

        jointPoints[PositionIndex.lShin.Int()].Threshold = config.ShinThreshold;
        jointPoints[PositionIndex.rShin.Int()].Threshold = config.ShinThreshold;
        jointPoints[PositionIndex.lShin.Int()].Smooth = config.ShinSmooth;
        jointPoints[PositionIndex.rShin.Int()].Smooth = config.ShinSmooth;
        jointPoints[PositionIndex.lShin.Int()].Ratio = config.ShinRatio;
        jointPoints[PositionIndex.rShin.Int()].Ratio = config.ShinRatio;

        jointPoints[PositionIndex.lHand.Int()].Threshold = config.ArmThreshold;
        jointPoints[PositionIndex.lThumb2.Int()].Threshold = config.ArmThreshold;
        jointPoints[PositionIndex.lMid1.Int()].Threshold = config.ArmThreshold;
        jointPoints[PositionIndex.rHand.Int()].Threshold = config.ArmThreshold;
        jointPoints[PositionIndex.rThumb2.Int()].Threshold = config.ArmThreshold;
        jointPoints[PositionIndex.rMid1.Int()].Threshold = config.ArmThreshold;

        jointPoints[PositionIndex.lHand.Int()].Smooth = config.ArmSmooth;
        jointPoints[PositionIndex.lThumb2.Int()].Smooth = config.ArmSmooth;
        jointPoints[PositionIndex.lMid1.Int()].Smooth = config.ArmSmooth;
        jointPoints[PositionIndex.rHand.Int()].Smooth = config.ArmSmooth;
        jointPoints[PositionIndex.rThumb2.Int()].Smooth = config.ArmSmooth;
        jointPoints[PositionIndex.rMid1.Int()].Smooth = config.ArmSmooth;

        jointPoints[PositionIndex.lHand.Int()].Ratio = config.ArmRatio;
        jointPoints[PositionIndex.lThumb2.Int()].Ratio = config.ArmRatio;
        jointPoints[PositionIndex.lMid1.Int()].Ratio = config.ArmRatio;
        jointPoints[PositionIndex.rHand.Int()].Ratio = config.ArmRatio;
        jointPoints[PositionIndex.rThumb2.Int()].Ratio = config.ArmRatio;
        jointPoints[PositionIndex.rMid1.Int()].Ratio = config.ArmRatio;
*/
        //是否锁定 ：点不点配置表都没用 因为这里写死了
        LockFoot = config.LockFoot == 1;
        LockLegs = config.LockLegs == 1;
        LockHand = config.LockHand == 1;
        jointPoints[PositionIndex.lToe.Int()].Lock = LockFoot || LockLegs;
        jointPoints[PositionIndex.rToe.Int()].Lock = LockFoot || LockLegs;
        jointPoints[PositionIndex.lFoot.Int()].Lock = LockFoot || LockLegs;
        jointPoints[PositionIndex.rFoot.Int()].Lock = LockFoot || LockLegs;
        jointPoints[PositionIndex.lShin.Int()].Lock = LockLegs;
        jointPoints[PositionIndex.rShin.Int()].Lock = LockLegs;
        jointPoints[PositionIndex.lHand.Int()].Lock = LockHand;
        jointPoints[PositionIndex.rHand.Int()].Lock = LockHand;
        //如果配置表上ElbowAxisTop不勾选就给rForearmBend和lForearmBend设定父亲 勾选就没有父亲 默认是0不勾选
        if (config.ElbowAxisTop == 0)
        {
            jointPoints[PositionIndex.rForearmBend.Int()].Parent = jointPoints[PositionIndex.rShldrBend.Int()];
            jointPoints[PositionIndex.lForearmBend.Int()].Parent = jointPoints[PositionIndex.lShldrBend.Int()];
        }
    else
        {
            jointPoints[PositionIndex.rForearmBend.Int()].Parent = null;
            jointPoints[PositionIndex.lForearmBend.Int()].Parent = null;
        }
    }

    private float tallHeadNeck;     //Head到Neck的长度
    private float tallNeckSpine;    //Neck到Spine的长度
    private float tallSpineCrotch;  //Spine到rThighBend和lThighBend之间的中点crotch的长度
    private float tallThigh;        //大腿平均长度
    private float tallShin;         //小腿平均长度
    public float EstimatedScore;
    private float VisibleThreshold = 0.05f;//关节点可见性阈值

    public void PoseUpdate()
    {
        // 计算Head到Neck的长度 Neck到Spine的长度
        tallHeadNeck = Vector3.Distance(jointPoints[PositionIndex.head.Int()].Pos3D, jointPoints[PositionIndex.neck.Int()].Pos3D);
        tallNeckSpine = Vector3.Distance(jointPoints[PositionIndex.neck.Int()].Pos3D, jointPoints[PositionIndex.spine.Int()].Pos3D);

        //比较关节点Score3D与阈值VisibleThreshold大小 判断关节点是否可见
        jointPoints[PositionIndex.lToe.Int()].Visibled = jointPoints[PositionIndex.lToe.Int()].Score3D < VisibleThreshold ? false : true;
        jointPoints[PositionIndex.rToe.Int()].Visibled = jointPoints[PositionIndex.rToe.Int()].Score3D < VisibleThreshold ? false : true;
        jointPoints[PositionIndex.lFoot.Int()].Visibled = jointPoints[PositionIndex.lFoot.Int()].Score3D < VisibleThreshold ? false : true;
        jointPoints[PositionIndex.rFoot.Int()].Visibled = jointPoints[PositionIndex.rFoot.Int()].Score3D < VisibleThreshold ? false : true;
        jointPoints[PositionIndex.lShin.Int()].Visibled = jointPoints[PositionIndex.lShin.Int()].Score3D < VisibleThreshold ? false : true;
        jointPoints[PositionIndex.rShin.Int()].Visibled = jointPoints[PositionIndex.rShin.Int()].Score3D < VisibleThreshold ? false : true;
        jointPoints[PositionIndex.lThighBend.Int()].Visibled = jointPoints[PositionIndex.lThighBend.Int()].Score3D < VisibleThreshold ? false : true;
        jointPoints[PositionIndex.rThighBend.Int()].Visibled = jointPoints[PositionIndex.rThighBend.Int()].Score3D < VisibleThreshold ? false : true;

        //首先判断左腿和右腿的膝盖和脚是否可见 根据是否可见计算小腿的平均长度（这个平均长度为可见的小腿总长度再除以腿数；两条腿都可见 就/2 只能见一条就/1）
        var leftShin = 0f;
        var rightShin = 0f;
        var shinCnt = 0;
        if (jointPoints[PositionIndex.lShin.Int()].Visibled && jointPoints[PositionIndex.lFoot.Int()].Visibled)
        {
            leftShin = Vector3.Distance(jointPoints[PositionIndex.lShin.Int()].Pos3D, jointPoints[PositionIndex.lFoot.Int()].Pos3D);
            shinCnt++;
        }
        if (jointPoints[PositionIndex.rShin.Int()].Visibled && jointPoints[PositionIndex.rFoot.Int()].Visibled)
        {
            rightShin = Vector3.Distance(jointPoints[PositionIndex.rShin.Int()].Pos3D, jointPoints[PositionIndex.rFoot.Int()].Pos3D);
            shinCnt++;
        }
        if (shinCnt != 0)
        {
            tallShin = (rightShin + leftShin) / shinCnt;
        }

        //首先判断左腿和右腿的大腿和膝盖是否可见 根据是否可见计算大腿的平均长度（程序写死了 默认大腿无论怎样都可见 除数也直接写死成了2）
        var rightThigh = 0f;
        var leftThigh = 0f;
        var thighCnt = 0;
        if (jointPoints[PositionIndex.rThighBend.Int()].Visibled && jointPoints[PositionIndex.rShin.Int()].Visibled)
        {
            rightThigh = Vector3.Distance(jointPoints[PositionIndex.rThighBend.Int()].Pos3D, jointPoints[PositionIndex.rShin.Int()].Pos3D);
            thighCnt++;
        }
        if (jointPoints[PositionIndex.lThighBend.Int()].Visibled && jointPoints[PositionIndex.lShin.Int()].Visibled)
        {
            leftThigh = Vector3.Distance(jointPoints[PositionIndex.lThighBend.Int()].Pos3D, jointPoints[PositionIndex.lShin.Int()].Pos3D);
            thighCnt++;
        }
        if (thighCnt != 0)
        {
            tallThigh = (rightThigh + leftThigh) / 2f;//这里写死了 thighCnt白定义了
        }

        // 计算Spine到rThighBend和lThighBend之间的中点crotch的长度
        var crotch = (jointPoints[PositionIndex.rThighBend.Int()].Pos3D + jointPoints[PositionIndex.lThighBend.Int()].Pos3D) / 2f;
        tallSpineCrotch = Vector3.Distance(jointPoints[PositionIndex.spine.Int()].Pos3D, crotch);

        //根据大腿和小腿的长度来调整它们的值，以确保它们不小于一个特定的阈值（0.01f）。贴近真实感
        if (tallThigh <= 0.01f && tallShin <= 0.01f)
        {
            tallThigh = tallNeckSpine;
            tallShin = tallNeckSpine;
        }
        else if (tallShin <= 0.01f)
        {
            tallShin = tallThigh;
        }
        else if (tallThigh <= 0.01f)
        {
            tallThigh = tallShin;
        }

        //计算角色高度并计算一些评分
        var t = tallHeadNeck + tallNeckSpine + tallSpineCrotch + (tallThigh + tallShin) * UpperBodyF;   //角色高度

        tall = t * 0.7f + prevTall * 0.3f;//新的高度是当前高度的 70% 加上先前高度的 30%
        prevTall = tall;//更新先前的高度为新的高度 

        //var dz = (centerTall - tall) / centerTall * ZScale;
        var dz = (tall / centerTall - 1f);//角色高度与中心高度的比例差异

        var score = 0f;
        var scoreCnt = 0;
        for (var i = 0; i < 24; i++)
        {
            if (!jointPoints[i].Visibled)
            {
                continue;
            }

            if (jointPoints[i].Child != null)//如果关节点有子节点，则累加其 Score3D 值到 score，并增加 scoreCnt 的计数
            {
                score += jointPoints[i].Score3D;
                scoreCnt++;
            }
        }

        if (scoreCnt > 0)//如果 scoreCnt 大于 0，则计算关节点的平均得分
        {
            EstimatedScore = score / scoreCnt;
        }
        else
        {
            EstimatedScore = 0f;
        }

        if(EstimatedScore < 0.03f)//如果 EstimatedScore 小于 0.03f，则返回，结束后续的操作
        {
            return;
        }
        //实现角色位置和姿态的更新
        //计算模型朝向
        var forward = TriangleNormal(jointPoints[PositionIndex.hip.Int()].Pos3D, jointPoints[PositionIndex.lThighBend.Int()].Pos3D, jointPoints[PositionIndex.rThighBend.Int()].Pos3D);
        //更新角色位置
        transform.position = jointPoints[PositionIndex.hip.Int()].Pos3D * movementScale + new Vector3(initPosition.x, initPosition.y, initPosition.z - dz * ZScale);
        //更新髋部关节点（PositionIndex.hip）的旋转角
        jointPoints[PositionIndex.hip.Int()].Transform.rotation = Quaternion.LookRotation(forward) * jointPoints[PositionIndex.hip.Int()].InverseRotation;

        // 所有关节点的旋转
        foreach (var jointPoint in jointPoints)
        {
            if (this.UpperBodyMode && !jointPoint.UpperBody)//如果处于上半身模式（UpperBodyMode），且当前关节点不属于上半身（!jointPoint.UpperBody），则跳过该关节点的旋转操作。
            {
                continue;
            }
            if (jointPoint.Lock)//如果关节点被锁定
            {
                if(LockLegs)//根据锁定腿部的设置（LockLegs）来判断是否进行旋转
                {
                    if(jointPoint.Index == PositionIndex.lShin || jointPoint.Index == PositionIndex.rShin)//如果关节点是左腿或右腿的关节点
                    {
                        jointPoint.Transform.rotation = Quaternion.LookRotation(Vector3.up, forward) * jointPoint.InverseRotation;//使用上方向（Vector3.up）和前方向（forward）计算旋转。
                    }
                }
                continue;
            }
            if (!jointPoint.Visibled)//如果关节点不可见（!jointPoint.Visibled），则跳过该关节点的旋转操作。
            {
                continue;
            }

            if (jointPoint.Parent != null)//如果关节点有父节点（jointPoint.Parent != null），根据父节点和当前节点的位置计算旋转。计算方法是使用父节点位置减去当前节点位置的向量（jointPoint.Parent.Pos3D - jointPoint.Pos3D）和当前节点和子节点的位置向量（jointPoint.Pos3D - jointPoint.Child.Pos3D）计算旋转。
            {
                var fv = jointPoint.Parent.Pos3D - jointPoint.Pos3D;
                jointPoint.Transform.rotation = Quaternion.LookRotation(jointPoint.Pos3D - jointPoint.Child.Pos3D, fv) * jointPoint.InverseRotation;
            }
            else if (jointPoint.Child != null)//如果关节点没有父节点，但有子节点（jointPoint.Child != null），并且子节点可见（jointPoint.Child.Visibled），则根据当前节点和子节点的位置向量（jointPoint.Pos3D - jointPoint.Child.Pos3D）和前方向（forward）计算旋转。
            {
                if (!jointPoint.Child.Visibled)
                {
                    continue;
                }
                jointPoint.Transform.rotation = Quaternion.LookRotation(jointPoint.Pos3D - jointPoint.Child.Pos3D, forward) * jointPoint.InverseRotation;
            }

        }
        /*
        if (jointPoints[PositionIndex.lFoot.Int()].Transform.position.y < FootIKY)
        {

            var origlFoot = jointPoints[PositionIndex.lFoot.Int()].Transform.position;
            var lShin = jointPoints[PositionIndex.lShin.Int()].Transform.position;
            var lThigh = jointPoints[PositionIndex.lThighBend.Int()].Transform.position;
            // Footの位置
            var afterT = (FootIKY - lThigh.y) / (origlFoot.y - lThigh.y);
            var afterX = afterT * (origlFoot.x - lThigh.x) + lThigh.x;
            var afterZ = afterT * (origlFoot.z - lThigh.z) + lThigh.z;
            var lFoot = new Vector3(afterX, FootIKY, afterZ);

            var nu1 = (lShin - lThigh).normalized;
            var nv1 = (lShin - origlFoot).normalized;
            var nn = Vector3.Cross(-nu1, nv1);

            var D = -1 * Vector3.Dot(nn, nv1) / Vector3.Dot(nn, nu1);
            var E = (Vector3.Dot(nn, lFoot) - Vector3.Dot(nn, lThigh)) / Vector3.Dot(nn, nu1);
            var U = nu1 * D + nv1;
            var N = nu1 * E + lThigh - lFoot;
            var r1 = (lThigh - lShin).magnitude;
            var r2 = (origlFoot - lShin).magnitude;
            var kaiA = U.sqrMagnitude * r1 * r1;
            var kaiB = Vector3.Dot(U, N) * r1;
            var kaiC = N.sqrMagnitude - r2 * r2;
            var sq = kaiB * kaiB - kaiA * kaiC;
            if (sq > 0)
            {
                Debug.Log("LFoo IK");
                sq = Mathf.Sqrt(sq);
                var sq1 = (-kaiB + sq) / kaiA;
                var sq2 = (-kaiB - sq) / kaiA;
                var shinPos1 = lShin;
                var shinPos2 = lShin;
                var flag1 = false;
                var flag2 = false;
                if (sq1 <= 1f && sq1 >= -1f)
                {
                    var kai1 = Mathf.Asin(sq1);
                    var sinT1 = Mathf.Sin(kai1);
                    var cosT1 = Mathf.Cos(kai1);
                    shinPos1 = lThigh + r1 * cosT1 * nu1 + r1 * sinT1 * nv1;
                    flag1 = true;
                }
                if (sq2 <= 1f && sq2 >= -1f)
                {
                    var kai2 = Mathf.Asin(sq2);
                    var sinT2 = Mathf.Sin(kai2);
                    var cosT2 = Mathf.Cos(kai2);
                    shinPos2 = lThigh + r1 * cosT2 * nu1 + r1 * sinT2 * nv1;
                    flag2 = true;
                }

                var lThighJP = jointPoints[PositionIndex.lThighBend.Int()];
                var lShinJP = jointPoints[PositionIndex.lShin.Int()];
                var lFootJP = jointPoints[PositionIndex.lFoot.Int()];
                if (flag1 && flag2)
                {
                    var nn1 = Vector3.Cross(-(shinPos1 - lThigh), (shinPos1 - lFoot)).normalized;
                    //var nn2 = Vector3.Cross(-(shinPos2 - lThigh), (shinPos2 - lFoot));
                    //if ((lShin - shinPos1).magnitude < (lShin - shinPos2).magnitude)
                    if ((nn - nn1).magnitude > 1.0f)
                    { 
                        lThighJP.Transform.rotation = Quaternion.LookRotation(lThigh - shinPos1, nn) * lThighJP.InverseRotation;
                        lShinJP.Transform.rotation = Quaternion.LookRotation(shinPos1 - lFoot, nn) * lShinJP.InverseRotation;
                        //lShinJP.Transform.position = shinPos1;
                        //lFootJP.Transform.position = lFoot;
                    }
                    else
                    {
                        lThighJP.Transform.rotation = Quaternion.LookRotation(lThigh - shinPos2, nn) * lThighJP.InverseRotation;
                        lShinJP.Transform.rotation = Quaternion.LookRotation(shinPos2 - lFoot, nn) * lShinJP.InverseRotation;
                        //lShinJP.Transform.position = shinPos2;
                        //lFootJP.Transform.position = lFoot;
                    }
                }
                
                else if (flag1)
                {
                //    lThighJP.Transform.rotation = Quaternion.LookRotation(lThigh - shinPos1, nn) * lThighJP.InverseRotation;
                //    lShinJP.Transform.rotation = Quaternion.LookRotation(shinPos1 - lFoot, nn) * lShinJP.InverseRotation;
                }
                else if (flag2)
                {
                 //   lThighJP.Transform.rotation = Quaternion.LookRotation(lThigh - shinPos2, nn) * lThighJP.InverseRotation;
                 //   lShinJP.Transform.rotation = Quaternion.LookRotation(shinPos2 - lFoot, nn) * lShinJP.InverseRotation;
                }
                
            }
        }
        */
        /*
        if (jointPoints[PositionIndex.lFoot.Int()].Transform.position.y < FootIKY)
        {
            var jpf = jointPoints[PositionIndex.lFoot.Int()];
            var fv = jpf.Parent.Transform.position - jpf.Transform.position;
            jpf.Transform.rotation = Quaternion.LookRotation(jpf.Transform.position - new Vector3(jpf.Child.Transform.position.x, FootIKY, jpf.Child.Transform.position.z), fv) * jpf.InverseRotation;
            var jpt = jointPoints[PositionIndex.lToe.Int()];
            jpt.Transform.rotation = Quaternion.LookRotation(jpt.Transform.position - new Vector3(jpt.Child.Transform.position.x, ToeIKY, jpt.Child.Transform.position.z), Vector3.up) * jpt.InverseRotation;

        }

        else  if (jointPoints[PositionIndex.lToe.Int()].Transform.position.y < ToeIKY)
        {
            var jp = jointPoints[PositionIndex.lToe.Int()];
            jp.Transform.rotation = Quaternion.LookRotation(jp.Transform.position - new Vector3(jp.Child.Transform.position.x, ToeIKY, jp.Child.Transform.position.z), Vector3.up) * jp.InverseRotation;

        }
        */

        // 实现头部旋转
        var gaze = jointPoints[PositionIndex.Nose.Int()].Pos3D - jointPoints[PositionIndex.head.Int()].Pos3D;//计算头部朝向
        var f = TriangleNormal(jointPoints[PositionIndex.Nose.Int()].Pos3D, jointPoints[PositionIndex.rEar.Int()].Pos3D, jointPoints[PositionIndex.lEar.Int()].Pos3D);//计算头部法向
        var head = jointPoints[PositionIndex.head.Int()];
        head.Transform.rotation = Quaternion.LookRotation(gaze, f) * head.InverseRotation;//头部旋转

        // 实现手腕旋转
        var lHand = jointPoints[PositionIndex.lHand.Int()];
        if (!lHand.Lock && lHand.Visibled)
        {
            var lf = TriangleNormal(lHand.Pos3D, jointPoints[PositionIndex.lMid1.Int()].Pos3D, jointPoints[PositionIndex.lThumb2.Int()].Pos3D);//计算左手手腕法向
            //计算左手手腕旋转
            lHand.Transform.rotation = Quaternion.LookRotation(jointPoints[PositionIndex.lThumb2.Int()].Pos3D - jointPoints[PositionIndex.lMid1.Int()].Pos3D, lf) * lHand.InverseRotation;
        }
        var rHand = jointPoints[PositionIndex.rHand.Int()];
        if (!rHand.Lock && rHand.Visibled)
        {
            var rf = TriangleNormal(rHand.Pos3D, jointPoints[PositionIndex.rThumb2.Int()].Pos3D, jointPoints[PositionIndex.rMid1.Int()].Pos3D);//计算右手手腕法向
            //计算右手手腕旋转
            rHand.Transform.rotation = Quaternion.LookRotation(jointPoints[PositionIndex.rThumb2.Int()].Pos3D - jointPoints[PositionIndex.rMid1.Int()].Pos3D, rf) * rHand.InverseRotation;
        }
        //设置骨架的显示位置
        foreach (var sk in Skeletons)
        {
            if (this.UpperBodyMode && !sk.upperBody)
            {
                continue;
            }

            var s = sk.start;
            var e = sk.end;

            sk.Line.SetPosition(0, new Vector3(s.Pos3D.x * SkeletonScale + SkeletonX, s.Pos3D.y * SkeletonScale + SkeletonY, s.Pos3D.z * SkeletonScale + SkeletonZ));
            sk.Line.SetPosition(1, new Vector3(e.Pos3D.x * SkeletonScale + SkeletonX, e.Pos3D.y * SkeletonScale + SkeletonY, e.Pos3D.z * SkeletonScale + SkeletonZ));
        }
    }
    
    //计算三角形法向
    Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;

        Vector3 dd = Vector3.Cross(d1, d2);
        dd.Normalize();

        return dd;
    }
    
    // 计算模型前向向量forward旋转到p1-p2的四元数
    private Quaternion GetInverse(JointPoint p1, JointPoint p2, Vector3 forward)
    {
        return Quaternion.Inverse(Quaternion.LookRotation(p1.Transform.position - p2.Transform.position, forward));//向量p1-p2旋转到forward有一个四元素，它的逆即为forward旋转到p1-p2的四元素
    }

    //创建一根骨头，将其添加到骨架列表
    //s 骨头起点 
    //e 骨头终点 
    //upperBody这根骨头是否属于上半身
    private void AddSkeleton(PositionIndex s, PositionIndex e, bool upperBody)
    {
        var sk = new Skeleton()
        {
            LineObject = new GameObject(this.name + "_Skeleton" +  (Skeletons.Count + 1).ToString("00")),
            start = jointPoints[s.Int()],
            end = jointPoints[e.Int()],
            upperBody = upperBody,
        };

        sk.Line = sk.LineObject.AddComponent<LineRenderer>();
        sk.Line.startWidth = 0.04f;
        sk.Line.endWidth = 0.01f;
        //顶点数固定为2
        sk.Line.positionCount = 2;
        sk.Line.material = SkeletonMaterial;

        Skeletons.Add(sk);
    }

    public static bool IsPoseUpdate = false;
   
    //帧更新
    private void Update()
    {
        if (jointPoints != null)
        {
            if (IsPoseUpdate)
            {
                PoseUpdate();
            }
            IsPoseUpdate = false;
        }
    }
    
    //IK设置
    void OnAnimatorIK()
    {
        //　IKを使わない場合はこれ以降なにもしない
        if (!useIK)
        {
            return;
        }

        //　アニメーションパラメータからIKのウエイトを取得
        rightFootWeight = 1f;
        leftFootWeight = 1f;
        //rightFootWeight = anim.GetFloat("RightFootWeight");
        //leftFootWeight = anim.GetFloat("LeftFootWeight");

        //　右足用のレイの視覚化
        Debug.DrawRay(anim.GetIKPosition(AvatarIKGoal.RightFoot) + rayPositionOffset, -transform.up * rayRange, Color.red);
        //　右足用のレイを飛ばす処理
        var ray = new Ray(anim.GetIKPosition(AvatarIKGoal.RightFoot) + rayPositionOffset, -transform.up);

        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayRange))
        {
            rightFootPos = hit.point;
            
            //　右足IKの設定
            anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, rightFootWeight);
            anim.SetIKPosition(AvatarIKGoal.RightFoot, rightFootPos + new Vector3(0f, offset, 0f));
            if (useIKRot)
            {
                rightFootRot = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
                anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, rightFootWeight);
                anim.SetIKRotation(AvatarIKGoal.RightFoot, rightFootRot);
            }
        }

        //　左足用のレイを飛ばす処理
        ray = new Ray(anim.GetIKPosition(AvatarIKGoal.LeftFoot) + rayPositionOffset, -transform.up);
        //　左足用のレイの視覚化
        Debug.DrawRay(anim.GetIKPosition(AvatarIKGoal.LeftFoot) + rayPositionOffset, -transform.up * rayRange, Color.red);

        if (Physics.Raycast(ray, out hit, rayRange))
        {
            leftFootPos = hit.point;

            //　左足IKの設定
            anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, leftFootWeight);
            anim.SetIKPosition(AvatarIKGoal.LeftFoot, leftFootPos + new Vector3(0f, offset, 0f));

            if (useIKRot)
            {
                leftFootRot = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
                anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, leftFootWeight);
                anim.SetIKRotation(AvatarIKGoal.LeftFoot, leftFootRot);
            }
        }
    }
}
