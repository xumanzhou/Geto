using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Geto.Rvt.Frm
{
    #region 模板类定义
    /// <summary>
    /// 作为基类或纯粹读取已有族实例的部分参数的值
    /// </summary>
    public class CFrm : CInst
    {
        public CFrm(FamilyInstance inst1) : base(inst1)
        {
            if (inst1 != null)
            {
                var lo1 = inst1.Location as LocationPoint;
                Position = lo1.Point;
                Rotation = lo1.Rotation;
            }
        }
        /// <summary>
        /// 族实例的插入点
        /// </summary>
        public XYZ Position { get; protected set; }
        /// <summary>
        /// 族实例的旋转弧度
        /// </summary>
        public double Rotation { get; protected set; }
        /// <summary>
        /// ‘铝模板完整名称’参数
        /// </summary>
        public CParamStr ParamName { get; } = new CParamStr("铝模板完整名称");
        /// <summary>
        /// ‘铝模板位置编号’参数
        /// </summary>
        public CParamStr ParamNumb { get; } = new CParamStr("铝模板位置编号");
        /// <summary>
        /// ‘图层’参数
        /// </summary>
        public CParamInt ParamLayer { get; } = new CParamInt("图层");
        /// <summary>
        /// ‘旧板’参数
        /// </summary>
        public CParamInt ParamOld { get; } = new CParamInt("旧板");
        public override CInst Clone()
        {
            var rt1 = base.Clone() as CFrm;
            rt1.Position = Position;
            rt1.Rotation = Rotation;
            return rt1;
        }
        public override bool Entmake()
        {
            foreach (var prop1 in GetType().GetProperties(BindingFlags.Static | BindingFlags.Public))
                if (prop1.PropertyType == typeof(CSymbol))
                {
                    var sym1 = ((CSymbol)prop1.GetValue(null)).Symbol;
                    if (!sym1.IsActive)
                        sym1.Activate();
                    Inst = sym1.Document.Create.NewFamilyInstance(XYZ.Zero, sym1, StructuralType.NonStructural);
                    using (var xl1 = Line.CreateUnbound(XYZ.Zero, XYZ.BasisZ))
                        Inst.Location.Rotate(xl1, Rotation);
                    Inst.Location.Move(Position);
                    WriteAllParams();
                    return true;
                }
            Err = new Exception($"Class Declaring Err\n{GetType().FullName}");
            Code = -1;
            return false;
        }
    }
    #endregion
    #region 模板计算
    /// <summary>
    /// 铝模板体系
    /// </summary>
    public enum FrmSystem : byte { Geto65, Geto635, Zw, Hb }
    /// <summary>
    /// 铝模配模区段划分的每个区段的种类信息
    /// </summary>
    public enum FrmCutRngType : byte { Null, Ic, Sc, Sn, Yc, Ycx, D, Dp, Dpt, Eb, Mb, K }
    /// <summary>
    /// 铝模配模区段划分的每个区段的边缘信息，如AV口
    /// </summary>
    public enum FrmCutEndType : byte { Null, A, V, F }
    public class CFrmCutRng : CErr
    {
        readonly FrmCutRngType m_Kind;
        /// <summary>
        /// 该区段用于配哪种模板
        /// </summary>
        public FrmCutRngType Kind => m_Kind;
        /// <summary>
        /// 该区段的单位长度
        /// </summary>
        public int MmLength { get; set; }
        /// <summary>
        /// 区段重复次数
        /// </summary>
        public int Count { get; set; }
        /// <summary>
        /// 起始孔位
        /// </summary>
        public int MmStartHole { get; set; }
        /// <summary>
        /// 该区段是否用于利旧
        /// </summary>
        public bool IsOld { get; set; }
        public CFrmCutRng(FrmCutRngType kind1, int len1, int count1 = 1)
        {
            m_Kind = kind1;
            MmLength = len1;
            Count = count1;
        }
    }
    public class CFrmCutRngSn : CFrmCutRng
    {
        /// <summary>
        /// 左端头样式
        /// </summary>
        public FrmCutEndType EndTypeA { get; set; }
        /// <summary>
        /// 右端头样式
        /// </summary>
        public FrmCutEndType EndTypeB { get; set; }
        public CFrmCutRngSn(FrmCutRngType type1, int len1, int count1 = 1, FrmCutEndType endKindL = FrmCutEndType.Null, FrmCutEndType endKindR = FrmCutEndType.Null) : base(type1, len1, count1)
        {
            EndTypeA = endKindL;
            EndTypeB = endKindR;
        }
    }
    ////
    //public class CSideSn : CSide
    //{
    //    /// <summary>
    //    /// 270°转角处的两个SN其中一个要延伸另一个的截面宽度、悬挑端头要延伸50等
    //    /// </summary>
    //    private int m_ExtendA, m_ExtendB;
    //    /// <summary>
    //    /// 根据轴网计算两端的尾数
    //    /// </summary>
    //    private int m_EndA, m_EndB;
    //    /// <summary>
    //    /// 区段划分的结果
    //    /// </summary>
    //    private LinkedList<CFrmCutRng> m_Cut;
    //    /// <summary>
    //    /// 截面宽度
    //    /// </summary>
    //    public int MmSectionWid { get; set; }
    //    /// <summary>
    //    /// 截面高度
    //    /// </summary>
    //    public int MmSectionHei { get; set; }
    //    public CSideSn(XYZ faceNormal, XYZ pt1, XYZ pt2) : base(faceNormal, pt1, pt2) { }
    //    public CSideSn(XYZ faceNormal, Curve l1) : base(faceNormal, l1) { }
    //    //
    //}
    //
    #endregion




    //示例
    /// <summary>
    /// 铝模W板竖向
    /// </summary>
    public class CFrmWV : CFrm
    {
        public static CSymbol Symbol { get; } = new CSymbol(BuiltInCategory.OST_GenericModel, "铝模W板竖向");
        public CFrmWV(XYZ pt1, double ro1, double mmWid, double mmHei) : base(null)
        {
            Position = pt1;
            Rotation = ro1;
            ParamWid.Value = mmWid;
            ParamHei.Value = mmHei;
        }
        public CFrmWV(FamilyInstance inst1) : base(inst1) { }
        /// <summary>
        /// ‘模板宽度’参数，毫米
        /// </summary>
        public CParamDbl ParamWid { get; } = new CParamDbl("模板宽度");
        /// <summary>
        /// ‘模板高度’参数，毫米
        /// </summary>
        public CParamDbl ParamHei { get; } = new CParamDbl("模板高度");
        //
    }
}
