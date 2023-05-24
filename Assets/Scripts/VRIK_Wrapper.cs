/*包装外部方法和属性，为VRIK提供外部接口
 该类从VRIK类继承，并添加了其他方法以更新VRIK解算器，设置IK位置权重，设置腿部位置权重和高度偏移。
 它的作用是为VRIK提供外部方法和属性的封装。该类还引用了FinalIK和UnityEngine的库
 */
using RootMotion.FinalIK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VRIK_Wrapper : VRIK
{
    public void UpdateVRIK(Transform transform)
    {
        References.AutoDetectReferences(transform, out references);
        solver.SetToReferences(references);

        base.UpdateSolver();
    }

    public void UpdateVRIK()
    {
        base.UpdateSolver();
    }

    public void SetIKPositionWeight(float weight)
    {
        this.solver.SetIKPositionWeight(weight);

    }

    public void SetLegPositionWeight(float weight)
    {
        this.solver.rightLeg.positionWeight = weight;
        this.solver.leftLeg.positionWeight = weight;
    }

    public void SetHeightOffset(float offset)
    {
        var ik = GetComponent<GrounderVRIK>();
        ik.solver.heightOffset = offset;
    }
}
