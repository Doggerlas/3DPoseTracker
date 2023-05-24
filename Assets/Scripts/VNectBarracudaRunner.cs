/*运行VNect模型，进行3D姿势识别
 这个程序文件是用于运行VNect模型的。它使用了Barracuda工具来处理图像，使用了视频捕捉器类来捕获视频流。
 它还使用了一个VNect模型，可以对图像进行姿势识别，并根据预测的姿势进行动画渲染。
 程序中还包括了一系列预测设置，包括平滑度、卡尔曼滤波、阈值等参数，用于改进预测。
 */
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using Unity.Barracuda;
using System.Text;

public class VNectBarracudaRunner : MonoBehaviour
{
    public NNModel NNModel;//用于存储神经网络模型的变量
    public WorkerFactory.Type WorkerType = WorkerFactory.Type.Auto;//指定工作器的类型，默认为自动选择。
    public bool Verbose = false;//是否输出详细信息

    public VNectModel VNectModel;// VNectModel类
    public VideoCapture videoCapture;//videoCapture类

    private Model _model; //_model _worker 用于加载和执行神经网络模型的变量。
    private IWorker _worker;
    private VNectModel.JointPoint[] jointPoints;//关节点
    private const int JointNum = 24;//关节点数量
    private const int JointNum_Squared = JointNum * 2;//关节点数量*2
    private const int JointNum_Cube = JointNum * 3;//关节点数量*3

    public int InputImageSize;//输入图像尺寸
    private float InputImageSizeF;//根据输入图像尺寸计算得到的辅助变量。
    private float InputImageSizeHalf;//根据输入图像尺寸计算得到的辅助变量。
    public int HeatMapCol; //热图的列数
    public int HeatMapCol_Half;//根据热图列数计算得到的辅助变量。
    private int HeatMapCol_Squared;//根据热图列数计算得到的辅助变量。
    private int HeatMapCol_Cube;//根据热图列数计算得到的辅助变量。
    private int HeatMapCol_JointNum;//根据热图列数计算得到的辅助变量。
    //用于存储热图和偏移量的数组
    private float[] heatMap2D;
    private float[] offset2D;
    private float[] heatMap3D;
    private float[] offset3D;
    private float unit;//单位尺寸
    //立方体偏移的辅助变量
    private int cubeOffsetLinear;
    private int cubeOffsetSquared;
    //控制帧率和等待时间的变量
    private bool Lock = true;
    private float waitSec = 1f / 30f;
    //计算帧率的变量
    private float elapsedMeasurementSec = 0f;
    private float fpsMeasurementSec = 0f;
    public float FPS = 0f;
    private int fpsCounter = 0;
    //用于控制滤波和卡尔曼滤波的参数。
    public bool UseLPF;
    public float Smooth;
    public bool UseKalmanF;
    public float KalmanParamQ;
    public float KalmanParamR;
    //前进和后退姿势的得分阈值。
    public float ForwardThreshold;
    public float BackwardThreshold;
    //滤波器的参数和滤波器列表
    public int NOrderLPF;
    private List<FIRFilter> filter = new List<FIRFilter>();
    private FilterWindow filterWindow = new FilterWindow();
    //用于更新 VNect 模型的委托和方法
    private delegate void UpdateVNectModelDelegate();
    private UpdateVNectModelDelegate UpdateVNectModel;
    //模型质量和高质量模型文件名
    public int ModelQuality = 1;
    private string HighQualityModelName = "HighQualityTrainedModel.nn";

    [SerializeField]
    private float EstimatedScore;//估计的姿势得分。
    //调试模式和控制输入和姿势模式的标志。
    public bool DebugMode;
    public bool User3Input;
    public bool UpperBodyMode;
    //用于写入文件的写入器对象
    StreamWriter writer;

    private void Start()
    {

        // デバッグ用ファイルを開く
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        //var csvPath = System.IO.Path.Combine(Application.streamingAssetsPath, "data.csv");
        //writer = new StreamWriter(csvPath, false, enc);

        //这段代码用于在调试模式下初始化模型，并根据用户输入选择使用异步更新模型还是同步更新模型。
        if (DebugMode)
        {
            if (User3Input)//如果用户输入（User3Input）为真，则将 UpdateVNectModel 设置为异步更新函数 UpdateVNectAsync 的委托。
            {
                UpdateVNectModel = new UpdateVNectModelDelegate(UpdateVNectAsync);
            }
            else//如果用户输入为假，则将 UpdateVNectModel 设置为同步更新函数 UpdateVNect 的委托(默认方式)
            {
                UpdateVNectModel = new UpdateVNectModelDelegate(UpdateVNect);
            }
            /*
            //该代码用于将模型数据写入文件。它将模型数据写入位于应用程序的 StreamingAssets 文件夹中的文件。如果需要将模型数据保存到文件中，可以取消注释并执行这段代码。
            var streamingPath = System.IO.Path.Combine(Application.streamingAssetsPath, HighQualityModelName);
            var writer = new BinaryWriter(new FileStream(streamingPath, FileMode.Create));
            writer.Write(NNModel.modelData.Value);
            writer.Close();
            */
            _model = ModelLoader.Load(NNModel, Verbose);//使用 ModelLoader.Load 函数加载模型（NNModel），并将加载的模型赋值给 _model
        }
        else//非调试模式下
        {
            var streamingPath = System.IO.Path.Combine(Application.streamingAssetsPath, HighQualityModelName);//读取Assets\StreamingAssets文件
            if (!File.Exists(streamingPath))//没有文件
            {
                ModelQuality = 0;
            }

            if (ModelQuality == 0)//Assets\StreamingAssets文件为空时
            {
                InputImageSize = 224;
                HeatMapCol = 14;
                User3Input = false;
                UpdateVNectModel = new UpdateVNectModelDelegate(UpdateVNect);//模型同步更新（默认方式）
                _model = ModelLoader.Load(NNModel, Verbose);
            }
            else
            {
                InputImageSize = 448;
                HeatMapCol = 28;
                User3Input = true;
                UpdateVNectModel = new UpdateVNectModelDelegate(UpdateVNectAsync);//模型异步更新
                _model = ModelLoader.LoadFromStreamingAssets(streamingPath);
            }
        }

        // Init VideoCapture
        videoCapture.Init(InputImageSize, InputImageSize);
        videoCapture.VideoReady += videoCapture_VideoReady;

        HeatMapCol_Half = HeatMapCol / 2;
        HeatMapCol_Squared = HeatMapCol * HeatMapCol;
        HeatMapCol_Cube = HeatMapCol * HeatMapCol * HeatMapCol;
        HeatMapCol_JointNum = HeatMapCol*JointNum;
        heatMap2D = new float[JointNum * HeatMapCol_Squared];
        offset2D = new float[JointNum * HeatMapCol_Squared * 2];
        heatMap3D = new float[JointNum * HeatMapCol_Cube];
        offset3D = new float[JointNum * HeatMapCol_Cube * 3];
        InputImageSizeF = InputImageSize ;
        InputImageSizeHalf = InputImageSizeF / 2f ;
        unit = 1f / (float)HeatMapCol;

        cubeOffsetLinear = HeatMapCol * JointNum_Cube;
        cubeOffsetSquared = HeatMapCol_Squared * JointNum_Cube;

        // Disabel sleep
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        _worker = WorkerFactory.CreateWorker(WorkerType, _model, Verbose);
        StartCoroutine("WaitLoad");

        var texture = new RenderTexture(InputImageSize, InputImageSize, 0, RenderTextureFormat.RGB565, RenderTextureReadWrite.sRGB)
        {
            useMipMap = false,
            autoGenerateMips = false,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
        };

        if (User3Input)
        {
            inputs[inputName_1] = new Tensor(texture, 3);
            inputs[inputName_2] = new Tensor(texture, 3);
            inputs[inputName_3] = new Tensor(texture, 3);
            _worker.Execute(inputs);
            inputs[inputName_1].Dispose();
            inputs[inputName_2].Dispose();
            inputs[inputName_3].Dispose();
        }
        else
        {
            input = new Tensor(texture, 3);
            _worker.Execute(input);
            input.Dispose();
        }
    }

    public void InitVNectModel(VNectModel avatar, ConfigurationSetting config)
    {
        VNectModel = avatar;
        jointPoints = VNectModel.Init(InputImageSize, config);

    }

    public void Exit()
    {
        Lock = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
      UnityEngine.Application.Quit();
#endif
    }

    public void SetVNectModel(VNectModel avatar)
    {
        VNectModel = avatar;
        jointPoints = avatar.JointPoints;

        VNectModel.Show();
    }

    public void VideoPlayStart(string path)
    {
        Lock = true;
        VNectModel.IsPoseUpdate = false;
        waitSec = 1f / videoCapture.SourceFps * 0.85f;

        StartCoroutine(videoCapture.VideoStart(path));
    }

    public void videoCapture_VideoReady()
    {
        Lock = false;
    }

    public void CameraPlayStart(int index)
    {
        Lock = false;
        waitSec = 1f / videoCapture.SourceFps * 0.85f;

        videoCapture.CameraPlayStart(index);
    }

    public void PlayStop()
    {
        Lock = true;
        videoCapture.PlayStop();
    }

    public void PlayPause()
    {
        Lock = true;
        videoCapture.Pause();
        //StartCoroutine(PlayPauseAsync());
    }
    public void Resume()
    {
        Lock = false;
        videoCapture.Resume();
    }
    /*
    private IEnumerator PlayPauseAsync()
    {
        yield return new WaitForSeconds(1f);
        videoCapture.Pause();
    }
    */
    public Vector3 GetHeadPosition()
    {
        return VNectModel.GetHeadPosition();
    }

    public void SetPredictSetting(ConfigurationSetting config)
    {
        Smooth = config.LowPassFilter;
        NOrderLPF = config.NOrderLPF;

        filterWindow.Init(config.FIROrderN03, config.FIRFromHz, config.FIRToHz, 30f);

        filter.Clear();
        for (var i = 0; i < JointNum; i++)
        {
            filter.Add(new FIRFilter(filterWindow, config.RangePathFilterBuffer03));
        }

        ForwardThreshold = config.ForwardThreshold;
        BackwardThreshold = config.BackwardThreshold;

        if(VNectModel != null)
        {
            VNectModel.SetPredictSetting(config);
        }
    }

    private void Update()
    {
        if (videoCapture.IsPlay())
        {
            var v = Time.deltaTime;
            elapsedMeasurementSec += v;
            fpsMeasurementSec += v;
            //if (elapsedMeasurementSec > waitSec)
            {
                UpdateVNectModel();

                if (fpsMeasurementSec >= 1f)
                {
                    FPS = (float)fpsCounter / fpsMeasurementSec;
                    fpsCounter = 0;
                    fpsMeasurementSec = 0f;
                }

                elapsedMeasurementSec = 0f;
            }
        }
    }
    //等待10s
    private IEnumerator WaitLoad()
    {
        yield return new WaitForSeconds(10f);
        Lock = false;
    }

    //通过异步方式更新VNect模型。它根据视频输入创建输入张量，并在模型推理之前进行相应的输入张量更新和释放
    private void UpdateVNectAsync()
    {
        input = new Tensor(videoCapture.MainTexture, 3);
        if (inputs[inputName_1] == null)
        {
            inputs[inputName_1] = input;
            inputs[inputName_2] = new Tensor(videoCapture.MainTexture, 3);
            inputs[inputName_3] = new Tensor(videoCapture.MainTexture, 3);
        }
        else
        {/*
            inputs[inputName_3].Dispose();
            inputs[inputName_1] = input;
            inputs[inputName_2] = input;
            inputs[inputName_3] = input;
            */
         /**/
            inputs[inputName_3].Dispose();

            inputs[inputName_3] = inputs[inputName_2];
            inputs[inputName_2] = inputs[inputName_1];
            inputs[inputName_1] = input;
            /* */
        }

        if (!Lock && videoCapture.IsPlay())
        {
            StartCoroutine(ExecuteModelAsync());
        }
    }


    //通过同步方式更新VNect模型。它根据视频输入创建输入张量，并在模型推理之前进行相应的输入张量更新和释放
    private void UpdateVNect()
    {
        ExecuteModel();
        PredictPose();

        fpsCounter++;
    }

    //模型推理过程，将视频输入传递给模型并获取输出结果。其中，offset3D 和 heatMap3D 是从模型的输出张量中下载的数据
    private void ExecuteModel()
    {

        // Create input and Execute model
        input = new Tensor(videoCapture.MainTexture, 3);//创建一个新的输入张量 input，使用 videoCapture.MainTexture 作为输入数据，通道数为 3
        _worker.Execute(input);//使用 _worker.Execute(input) 执行模型推理。这里 _worker 是模型执行器，用于执行模型的推理过程。
        input.Dispose();//释放输入张量 input

        //将输出张量的数据下载到相应的变量中。
        for (var i = 2; i < _model.outputs.Count; i++)
        {
            b_outputs[i] = _worker.PeekOutput(_model.outputs[i]);
        }
        //在这段代码中，注释掉了 heatMap2D 和 offset2D 的下载，而下载了 offset3D 和 heatMap3D 的数据。
        // Get data from outputs
        //heatMap2D = b_outputs[0].data.Download(b_outputs[0].shape);
        //offset2D = b_outputs[1].data.Download(b_outputs[1].shape);
        offset3D = b_outputs[2].data.Download(b_outputs[2].shape);
        heatMap3D = b_outputs[3].data.Download(b_outputs[3].shape);
    }

    
    private const string inputName_1 = "input.1";
    private const string inputName_2 = "input.4";
    private const string inputName_3 = "input.7";
    /*
    private const string inputName_1 = "input.1";
    private const string inputName_2 = "input.3";
    private const string inputName_3 = "input.5";
    */
    /*
    private const string inputName_1 = "0";
    private const string inputName_2 = "1";
    private const string inputName_3 = "2";
    */

    Tensor input = new Tensor();
    Dictionary<string, Tensor> inputs = new Dictionary<string, Tensor>() { { inputName_1, null }, { inputName_2, null }, { inputName_3, null }, };
    Tensor[] b_outputs = new Tensor[4];

    //预测关节点位置
    private void PredictPose()
    {
        var score = 0f;
        //var csv = videoCapture.VideoPlayer.frame.ToString();
        filterWindow.SetFps(FPS);
       
        for (var j = 0; j < JointNum; j++)
        {
            //遍历三维热图的坐标轴，找到每个关节点在三维热图中的最高得分和对应的索引
            var jp = jointPoints[j];
            var maxXIndex = 0;
            var maxYIndex = 0;
            var maxZIndex = 0;
            jp.Score3D = 0.0f;
            var jj = j * HeatMapCol;

            for (var z = 0; z < HeatMapCol; z++)
            {
                var zz = jj + z;
                for (var y = 0; y < HeatMapCol; y++)
                {
                    var yy = y * HeatMapCol_Squared * JointNum + zz;
                    for (var x = 0; x < HeatMapCol; x++)
                    {
                        float v = heatMap3D[yy + x * HeatMapCol_JointNum];
                        if (v > jp.Score3D)
                        {
                            jp.Score3D = v;//最高得分和对应的索引
                            maxXIndex = x;
                            maxYIndex = y;
                            maxZIndex = z;
                        }
                    }
                }
            }
            //更新关节点的位置信息
            //jp.PrevNow3D = jp.Now3D;
            score += jp.Score3D;
            var yi = maxYIndex * cubeOffsetSquared + maxXIndex * cubeOffsetLinear;
            //更新关节点的x y z坐标
            jp.Now3D.x = ((offset3D[yi + jj + maxZIndex] + 0.5f + (float)maxXIndex) / (float)HeatMapCol) * InputImageSizeF - InputImageSizeHalf;
            jp.Now3D.y = InputImageSizeF - ((offset3D[yi + (j + JointNum) * HeatMapCol + maxZIndex] + 0.5f + (float)maxYIndex) / (float)HeatMapCol) * InputImageSizeF - InputImageSizeHalf;
            jp.Now3D.z = ((offset3D[yi + (j + JointNum_Squared) * HeatMapCol + maxZIndex] + 0.5f + (float)(maxZIndex - HeatMapCol_Half)) / (float)HeatMapCol) * InputImageSizeF;
            ////(jp.Now3D.x, jp.Now3D.y, jp.Now3D.z) = filter[j].Add(fx, fy, fz, FPS);
            (jp.Now3D.x, jp.Now3D.y, jp.Now3D.z) = filter[j].Add(jp.Now3D.x, jp.Now3D.y, jp.Now3D.z, FPS);//使用滤波器 filter[j] 对更新后的坐标进行平滑处理，将平滑后的坐标存储回 jp.Now3D.x、jp.Now3D.y 和 jp.Now3D.z。

        }
        //writer.WriteLine(csv);

        EstimatedScore = score / JointNum;

        // 计算髋部位置
        var lc = (jointPoints[PositionIndex.rThighBend.Int()].Now3D + jointPoints[PositionIndex.lThighBend.Int()].Now3D) / 2f;
        jointPoints[PositionIndex.hip.Int()].Now3D = (jointPoints[PositionIndex.abdomenUpper.Int()].Now3D + lc) / 2f;
        // 计算颈部位置
        jointPoints[PositionIndex.neck.Int()].Now3D = (jointPoints[PositionIndex.rShldrBend.Int()].Now3D + jointPoints[PositionIndex.lShldrBend.Int()].Now3D) / 2f;
        // 计算头部位置
        var cEar = (jointPoints[PositionIndex.rEar.Int()].Now3D + jointPoints[PositionIndex.lEar.Int()].Now3D) / 2f;
        var hv = cEar - jointPoints[PositionIndex.neck.Int()].Now3D;
        var nhv = Vector3.Normalize(hv);
        var nv = jointPoints[PositionIndex.Nose.Int()].Now3D - jointPoints[PositionIndex.neck.Int()].Now3D;
        jointPoints[PositionIndex.head.Int()].Now3D = jointPoints[PositionIndex.neck.Int()].Now3D + nhv * Vector3.Dot(nhv, nv);
        // 计算脊柱位置
        jointPoints[PositionIndex.spine.Int()].Now3D = jointPoints[PositionIndex.abdomenUpper.Int()].Now3D;

        // Filters
        //
        //对关节点位置进行滤波处理
        var frwd = TriangleNormal(jointPoints[PositionIndex.hip.Int()].Now3D, jointPoints[PositionIndex.lThighBend.Int()].Now3D, jointPoints[PositionIndex.rThighBend.Int()].Now3D);
        var frwdAngle = Vector3.Angle(frwd, Vector3.back);
 
        foreach (var jp in jointPoints)
        {
            KalmanUpdate(jp); //对每个关节点进行卡尔曼滤波更新
            //使用一阶低通滤波器对关节点位置进行平滑处理，将结果存储在 PrevPos3D 和 Pos3D 属性中。
            jp.PrevPos3D[0] = jp.Pos3D;
            for (var i = 1; i < NOrderLPF; i++)//NOrderLPF=7
            {
                //jp.PrevPos3D[i] = jp.PrevPos3D[i] * Smooth + jp.PrevPos3D[i - 1] * (1f - Smooth);
                jp.PrevPos3D[i] = jp.PrevPos3D[i] * Smooth + jp.PrevPos3D[i - 1] * (1f - Smooth);
            }
            jp.Pos3D = jp.PrevPos3D[NOrderLPF - 1];

 
            jp.Visibled = true;
        }

        if (frwdAngle < 45f)//如果 frwdAngle 小于 45 度，并且 EstimatedScore 大于 ForwardThreshold，则将 VNectModel.IsPoseUpdate 设置为 true，表示需要更新姿势。
        {
            if (EstimatedScore > ForwardThreshold)
            {
                VNectModel.IsPoseUpdate = true;
            }
        }
        else//如果 frwdAngle 大于等于 45 度，并且 EstimatedScore 大于 BackwardThreshold，则将 VNectModel.IsPoseUpdate 设置为 true。
        {
            if (EstimatedScore > BackwardThreshold)
            {
                VNectModel.IsPoseUpdate = true;
            }
        }
    }

    private IEnumerator ExecuteModelAsync()
    {
        if (Lock)
        {
            yield return null;
        }

        // Create input and Execute model
        yield return _worker.StartManualSchedule(inputs);

        if (!Lock)
        {
            // Get outputs
            for (var i = 2; i < _model.outputs.Count; i++)
            {
                b_outputs[i] = _worker.PeekOutput(_model.outputs[i]);
            }

            // Get data from outputs
            //heatMap2D = b_outputs[0].data.Download(b_outputs[0].shape);
            //offset2D = b_outputs[1].data.Download(b_outputs[1].shape);
            offset3D = b_outputs[2].data.Download(b_outputs[2].shape);
            heatMap3D = b_outputs[3].data.Download(b_outputs[3].shape);

            PredictPose();

            fpsCounter++;
        }
    }


    Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;

        Vector3 dd = Vector3.Cross(d1, d2);
        dd.Normalize();

        return dd;
    }
    /*
    bool FrontBackCheckv(VNectModel.JointPoint jp1, VNectModel.JointPoint jp2, bool flag)
    {
        var l1 = Vector3.Distance(jp1.PrevNow3D, jp1.Now3D);
        var c1 = Vector3.Distance(jp2.PrevNow3D, jp1.Now3D);

        var l2 = Vector3.Distance(jp2.PrevNow3D, jp2.Now3D);
        var c2 = Vector3.Distance(jp1.PrevNow3D, jp2.Now3D);

        if(l1 > c1 && l2 > c2)
        {
            jp1.Error++;
            jp2.Error++;
            if (!flag && jp1.Error == jp1.RattlingCheckFrame)
            {
                jp1.Error = 0;
                jp2.Error = 0;
                return false;
            }

            return true;
        }
        else
        {
            jp1.Error = 0;
            jp2.Error = 0;

            return false;
        }
    }
    */
    //卡尔曼滤波器的更新步骤，用于对关节点的测量进行滤波
    void KalmanUpdate(VNectModel.JointPoint measurement)
    {
        measurementUpdate(measurement);
        measurement.Pos3D.x = measurement.X.x + (measurement.Now3D.x - measurement.X.x) * measurement.K.x;
        measurement.Pos3D.y = measurement.X.y + (measurement.Now3D.y - measurement.X.y) * measurement.K.y;
        measurement.Pos3D.z = measurement.X.z + (measurement.Now3D.z - measurement.X.z) * measurement.K.z;
        measurement.X = measurement.Pos3D;
    }

    //卡尔曼滤波器的状态更新，用于更新卡尔曼增益和误差协方差矩阵
    void measurementUpdate(VNectModel.JointPoint measurement)
    {
        //计算关节点在 x y z轴方向上的卡尔曼增益 K.x K.y K.z
        measurement.K.x = (measurement.P.x + KalmanParamQ) / (measurement.P.x + KalmanParamQ + KalmanParamR);
        measurement.K.y = (measurement.P.y + KalmanParamQ) / (measurement.P.y + KalmanParamQ + KalmanParamR);
        measurement.K.z = (measurement.P.z + KalmanParamQ) / (measurement.P.z + KalmanParamQ + KalmanParamR);
        //更新关节点在 x y z 轴方向上的误差协方差矩阵 P.x P.y P.z
        measurement.P.x = KalmanParamR * (measurement.P.x + KalmanParamQ) / (KalmanParamR + measurement.P.x + KalmanParamQ);
        measurement.P.y = KalmanParamR * (measurement.P.y + KalmanParamQ) / (KalmanParamR + measurement.P.y + KalmanParamQ);
        measurement.P.z = KalmanParamR * (measurement.P.z + KalmanParamQ) / (KalmanParamR + measurement.P.z + KalmanParamQ);
    }

    private void OnDestroy()
    {
        _worker?.Dispose();

        if (User3Input)
        {
            // Assuming model with multiple inputs that were passed as a Dictionary
            foreach (var key in inputs.Keys)
            {
                inputs[key].Dispose();
            }

            inputs.Clear();
        }
        else
        {
            input.Dispose();
        }
    }
}
