using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Geto.Rvt
{
    public static class CRvt
    {
        /// <summary>
        /// Parameter差或XYZ.DistanceTo小于该值时认为是重叠
        /// </summary>
        public static double ToleranceInchLength { get; set; } = 1 / 304.8;
        /// <summary>
        /// 每次配模之前要先对FamilySymbol进行初始化
        /// </summary>
        /// <param name="types">每个Type必须继承自CFrm且存在public static CSymbol属性</param>
        /// <returns>是否初始化成功</returns>
        public static bool InitSymbol(this Document doc, params Type[] types)
        {
            var dics = new Dictionary<Type, CSymbol>();
            foreach (var type1 in types)
            {
                var bool1 = false;
                foreach (var prop1 in type1.GetProperties(BindingFlags.Static | BindingFlags.Public))//每个类中都必须有public static CSymbol属性
                    if (prop1.PropertyType == typeof(CSymbol))
                    {
                        if (prop1.CanRead)
                        {
                            bool1 = true;
                            dics.Add(type1, (CSymbol)prop1.GetValue(null));
                        }
                        break;
                    }
                if (!bool1)
                {
                    TaskDialog.Show(CGeto.SysName, $"Class Declaring Err\n{type1.FullName}");
                    dics.Clear();
                    return false;
                }
            }
            using (var filter1 = new ElementMulticategoryFilter(dics.Values.Select(i => i.Category).ToArray()))
            using (var co1 = new FilteredElementCollector(doc))
            {
                co1.OfClass(typeof(FamilySymbol)).WherePasses(filter1);
                foreach (var type1 in dics)
                {
                    var bool1 = false;
                    foreach (FamilySymbol sym1 in co1)
                        if (sym1.Category.Id.IntegerValue == (int)type1.Value.Category)
                            if (sym1.Name == type1.Value.SymbolName)
                                if (sym1.Family.Name == type1.Value.FamilyName)
                                {
                                    type1.Value.Symbol = sym1;
                                    bool1 = true;
                                    break;
                                }
                    if (!bool1)
                    {
                        TaskDialog.Show(CGeto.SysName, $"please load familysymbol first\n{type1.Value.FamilyName}-{type1.Value.SymbolName}\n{type1.Key.FullName}");
                        dics.Clear();
                        return false;
                    }
                }
            }
            dics.Clear();
            return true;
        }
        /// <summary>
        /// 英寸长度转毫米长度并取整
        /// </summary>
        public static int ToMmLength(this double inchLen, int baseLen = 1) => Convert.ToInt32(inchLen * 304.8 / baseLen) * baseLen;
        /// <summary>
        /// 计算点在射线上的垂足
        /// </summary>
        public static UV ProjectTo(this UV pt1, UV linePt, UV lineDir) => linePt + lineDir * lineDir.DotProduct(pt1 - linePt);
        /// <summary>
        /// 计算点在射线上的垂足
        /// </summary>
        public static XYZ ProjectTo(this XYZ pt1, XYZ linePt, XYZ lineDir) => linePt + lineDir * lineDir.DotProduct(pt1 - linePt);
        /// <summary>
        /// 计算点在面上的垂足
        /// </summary>
        public static XYZ ProjectTo(this XYZ pt1, PlanarFace face1) => face1.Origin.ProjectTo(pt1, face1.FaceNormal);
        /// <summary>
        /// 计算点在面上的垂足
        /// </summary>
        public static XYZ ProjectTo(this XYZ pt1, Plane face1, bool isPt) => isPt ? face1.Origin.ProjectTo(pt1, face1.Normal) : pt1.Add(Transform.CreateReflection(face1).OfVector(pt1)).Divide(2);
        /// <summary>
        /// 构建Plane，方便不同版本的API减少配置
        /// </summary>
        /// <param name="pt1">Plane上的任意一点，作为Plane的原点</param>
        /// <param name="pt2">Plane的法向量或第二个点用来确定法向量</param>
        /// <param name="pt2IsPoint">前一个参数是否点，可能是向量也可能是点</param>
        public static Plane NewPlaneTo(this XYZ pt1, XYZ pt2, bool pt2IsPoint)
        {
            if (pt2IsPoint)
                return pt1.NewPlaneTo(pt2 - pt1, !pt2IsPoint);
            else
                return new Plane(pt2, pt1);
        }
        /// <summary>
        /// 获取PlanarFace所在的Plane
        /// </summary>
        public static Plane NewPlane(this PlanarFace pface1) => pface1.Origin.NewPlaneTo(pface1.FaceNormal, false);
        public static List<T> GetAll<T>(this Element elem1, Options op1 = null) where T : GeometryObject
        {
            //ComputeReferences = true时的Face可用于生成基于面的族
            if (op1 == null) op1 = new Options() { View = elem1.Document.ActiveView };
            var rt1 = new List<T>();
            foreach (var geo1 in elem1.get_Geometry(op1))
            {
                if (geo1 is GeometryInstance inst1)
                    rt1.AddRange(inst1.GetAll<T>());
                else if (geo1 is T t1)
                    rt1.Add(t1);
            }
            return rt1;
        }
        public static List<T> GetAll<T>(this GeometryInstance inst1) where T : GeometryObject
        {
            var rt1 = new List<T>();
            foreach (var geo1 in inst1.GetInstanceGeometry())
            {
                if (geo1 is GeometryInstance inst2)
                    rt1.AddRange(inst2.GetAll<T>());
                else if (geo1 is T t1)
                    rt1.Add(t1);
            }
            return rt1;
        }
        public static bool IsKindOf(this Element elem1, params BuiltInCategory[] cats) => cats.Contains((BuiltInCategory)elem1.Category.Id.IntegerValue);
        public static bool IsKindOf(this FamilyInstance inst1, params StructuralType[] types) => types.Contains(inst1.StructuralType);
        /// <summary>
        /// 根据轴网计算墙线、IC线、SN线两边的尾数
        /// </summary>
        /// <param name="endL">起始端的尾数</param>
        /// <param name="endR">结束端的尾数</param>
        public static bool GetMantissa(this IEnumerable<Line> boundAxiss, Line boundLine, out int endL, out int endR, int extendL = 0, int extendR = 0)
        {
            endL = endR = 0;
            var mas = new List<Tuple<double, Line>>();
            foreach (var axis1 in boundAxiss)
                if (boundLine.Direction.AngleTo(axis1.Direction).ToAngle() == 90 &&//与轴线垂直
                    axis1.Direction.DotProduct(boundLine.Origin - axis1.GetEndPoint(0)) > 0 &&//在轴线范围内
                    axis1.Direction.DotProduct(boundLine.Origin - axis1.GetEndPoint(1)) < 0)//在轴线范围内
                    mas.Add(Tuple.Create(Math.Abs(boundLine.Direction.DotProduct(axis1.Origin - (boundLine.GetEndPoint(0) + boundLine.GetEndPoint(1)) / 2)), axis1));//到线中点的距离
            if (mas.Count == 0) return false;//没有与之匹配的轴线则失败
            var l1 = mas.OrderBy(i => i.Item1).First().Item2;//到线中点的距离最近的轴线作为参照
            endL = boundLine.Direction.DotProduct(boundLine.GetEndPoint(0) - l1.Origin).ToMmLength() - extendL;
            while (endL < 0) endL += 50000;
            endL %= 50;
            if (endL != 0) endL = 50 - endL;
            endR = boundLine.Direction.DotProduct(boundLine.GetEndPoint(1) - l1.Origin).ToMmLength() + extendR;
            while (endR < 0) endR += 50000;
            endR %= 50;
            return true;
        }
        /// <summary>
        /// 根据轴网计算SN截面宽度的尾数
        /// </summary>
        /// <param name="secondDerivative">SN侧面所在面的法向量</param>
        /// <param name="end">截面宽度的尾数</param>
        public static bool GetMantissa(this IEnumerable<Line> boundAxiss, Line line, XYZ secondDerivative, out int end)
        {
            end = 0;
            var dists = new List<int>();
            foreach (var axis1 in boundAxiss)
            {
                if (axis1.Direction.AngleTo(line.Direction).ToAngle() % 180 > 0) continue;//必须平行
                var paras = new double[] {
                    axis1.Direction.DotProduct(line.GetEndPoint(0) - axis1.Origin),
                    axis1.Direction.DotProduct(line.GetEndPoint(1) - axis1.Origin) };
                Array.Sort(paras);
                if (paras[0] > axis1.GetEndParameter(1)) continue;//必须有公共区域
                if (paras[1] < axis1.GetEndParameter(0)) continue;//必须有公共区域
                dists.Add(secondDerivative.DotProduct(axis1.Origin - line.Origin).ToMmLength());
            }
            if (dists.Count == 0) return false;
            end = dists.OrderBy(i => Math.Abs(i)).First();//距离最近的轴线作为参照
            while (end < 0) end += 50000;
            end %= 50;
            return true;
        }
        /// <summary>
        /// 根据轴网计算单边尾数，比如计算IC单侧的尾数
        /// </summary>
        public static bool GetMantissa(this IEnumerable<Line> boundAxiss, XYZ pt1, XYZ dir1, out int end)
        {
            end = 0;
            var dists = new List<int>();
            foreach (var axis1 in boundAxiss)
                if (dir1.AngleTo(axis1.Direction).ToAngle() == 90 &&//与轴线垂直
                    axis1.Direction.DotProduct(pt1 - axis1.GetEndPoint(0)) > 0 &&//在轴线范围内
                    axis1.Direction.DotProduct(pt1 - axis1.GetEndPoint(1)) < 0)//在轴线范围内
                    dists.Add(dir1.DotProduct(axis1.Origin - pt1).ToMmLength());
            if (dists.Count == 0) return false;
            end = dists.OrderBy(i => Math.Abs(i)).First();//距离最近的轴线作为参照
            while (end < 0) end += 50000;
            end %= 50;
            return true;
        }
        //
    }
    /// <summary>
    /// Rvt的FamilySymbol信息，只允许存在于CFrm的子类中 且只能是公共静态属性
    /// </summary>
    public sealed class CSymbol
    {
        readonly BuiltInCategory m_Cat;
        readonly string m_SymbolName;
        readonly string m_FamilyName;
        internal BuiltInCategory Category => m_Cat;
        internal string SymbolName => m_SymbolName;
        internal string FamilyName => m_FamilyName;
        public FamilySymbol Symbol { get; internal set; }
        public CSymbol(BuiltInCategory cat1, string symbolName, string familyName = default)
        {
            m_Cat = cat1;
            m_SymbolName = symbolName;
            m_FamilyName = string.IsNullOrEmpty(familyName) ? symbolName : familyName;
        }
    }
    /// <summary>
    /// Rvt的FamilyInstance信息
    /// </summary>
    public abstract class CInst : CErr
    {
        #region 静态成员
        static bool g_AutoCollect;
        static readonly LinkedList<CInst> m_Elems = new LinkedList<CInst>();
        /// <summary>
        /// AutoCollect为True时自动收集最近构建的类实例。AutoCollect赋值时自动清空
        /// </summary>
        public static IEnumerable<CInst> RecentCollection => m_Elems;
        /// <summary>
        /// 新建实例时是否自动收集，免得还要另外构建容器来收集。每次赋值时自动清空容器
        /// </summary>
        public static bool AutoCollect { set { g_AutoCollect = value; m_Elems.Clear(); } }
        #endregion
        #region 实例内容
        readonly CParam[] m_AllParams;
        public FamilyInstance Inst { get; protected set; }//可能基于点也可能基于线
        protected CInst(FamilyInstance inst1)
        {
            if (g_AutoCollect)
                lock (m_Elems)//新建数据时可能多线程
                    m_Elems.AddLast(this);
            m_AllParams =
                GetType().
                GetProperties(BindingFlags.Instance | BindingFlags.Public).
                Where(i => i.PropertyType.IsSubclassOf(typeof(CParam))).
                Select(i => (CParam)i.GetValue(this)).
                ToArray();
            var allField = typeof(CParam).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var para1 in m_AllParams)
                allField[1].SetValue(para1, this);//注意：必须确保字段的顺序
            if (inst1 != null)
            {
                Inst = inst1;
                foreach (var para1 in m_AllParams)
                {
                    var fromPara1 = inst1.LookupParameter(para1.Name);
                    allField[2].SetValue(para1, fromPara1);//注意：必须确保字段的顺序
                    if (para1 is CParamInt int1)
                        int1.Value = fromPara1.AsInteger();
                    else if (para1 is CParamId id1)
                        id1.Value = fromPara1.AsElementId();
                    else if (para1 is CParamStr str1)
                        str1.Value = fromPara1.AsString();
                    else if (para1 is CParamDbl real1)
                    {
                        real1.Value = fromPara1.AsDouble();
                        if (real1.ConvertUnit)
                            switch (fromPara1.Definition.ParameterType)
                            {
                                case ParameterType.Angle:
                                    real1.Value = Math.Round(real1.Value * 180 / Math.PI, 3) % 360;
                                    break;
                                case ParameterType.Length:
                                    real1.Value = Math.Round(real1.Value * 304.8, 3);
                                    break;
                                case ParameterType.Area:
                                    real1.Value = Math.Round(real1.Value * 304.8 * 304.8, 3);
                                    break;
                                case ParameterType.Volume:
                                    real1.Value = Math.Round(real1.Value * 304.8 * 304.8 * 304.8, 3);
                                    break;
                            }
                    }
                }
            }
        }
        /// <summary>
        /// 复制一个类实例。该方法只进行了Element复制和Parameter读取，额外操作需要子类型重写覆盖该方法
        /// </summary>
        public virtual CInst Clone()
        {
            foreach (var fun1 in GetType().GetConstructors(BindingFlags.Instance | BindingFlags.Public))
            {
                var paras = fun1.GetParameters();
                if (paras.Length == 1)//注意子类型必须存在一个参数的构造函数
                {
                    var type1 = paras[0].ParameterType;
                    if (type1 == typeof(FamilyInstance))
                    {
                        FamilyInstance inst1 = null;
                        if (Inst != null) inst1 = Inst.Document.GetElement(LinearArray.ArrayElementWithoutAssociation(Inst.Document, Inst.Document.ActiveView, Inst.Id, 2, XYZ.Zero, ArrayAnchorMember.Second).Last()) as FamilyInstance;//原位复制
                        var rt1 = (CInst)fun1.Invoke(new object[] { inst1 });
                        for (var i = m_AllParams.Length - 1; i >= 0; i--)
                            if (rt1.m_AllParams[i] is CParamInt int1)
                                int1.Value = ((CParamInt)m_AllParams[i]).Value;
                            else if (rt1.m_AllParams[i] is CParamId id1)
                                id1.Value = ((CParamId)m_AllParams[i]).Value;
                            else if (rt1.m_AllParams[i] is CParamStr str1)
                                str1.Value = ((CParamStr)m_AllParams[i]).Value;
                            else if (rt1.m_AllParams[i] is CParamDbl real1)
                                real1.Value = ((CParamDbl)m_AllParams[i]).Value;
                        return rt1;
                    }
                }
            }
            throw new Exception("Type Define Err!\n" + GetType().FullName);
        }
        /// <summary>
        /// 根据内存信息生成实际Element，子类根据需要而重写该方法，有些子类只需读取信息就不用重写该方法
        /// </summary>
        public virtual bool Entmake() => throw new Exception("Please Override 'Entmake'!\n" + GetType().FullName);
        /// <summary>
        /// 图元修改后更新，如模板刷新
        /// </summary>
        public virtual bool Update() => throw new Exception("Please Override 'Update'!\n" + GetType().FullName);
        /// <summary>
        /// 对齐拉伸
        /// </summary>
        public virtual bool Stretch() => throw new Exception("Please Override 'Stretch'!\n" + GetType().FullName);
        /// <summary>
        /// 翻转，解决镜像问题
        /// </summary>
        public virtual bool Flip() => throw new Exception("Please Override 'Flip'!\n" + GetType().FullName);
        /// <summary>
        /// 将所有RvtParam的值写入到Element中，必须先有Element
        /// </summary>
        public void WriteAllParams() { if (Inst != null) foreach (var i in m_AllParams) i.Write(); }
        #endregion
    }
    /// <summary>
    /// Rvt参数信息。该类的子类只能存在于CInst的子类中 且只能是公共实例属性
    /// </summary>
    public abstract class CParam
    {
        readonly string m_Name;
        private CInst m_Inst;//该类实例属于哪个CInst的类实例。构造CInst时通过映射的方法来赋值
        private Parameter m_Param;//与该内存信息关联的族实例参数。从已有族实例构造CInst时会自动赋值，根据内存信息生成图形时在Write函数中赋值
        public string Name => m_Name;
        protected Parameter Host => m_Param;
        protected CParam(string name1) => m_Name = name1;
        /// <summary>
        /// 将内存信息写入族实例的参数中
        /// </summary>
        public virtual void Write()
        {
            if (m_Param == null)
                m_Param = m_Inst.Inst.LookupParameter(m_Name);
        }
    }
    /// <summary>
    /// Integer类型的参数
    /// </summary>
    public class CParamInt : CParam
    {
        public int Value { get; set; }
        public CParamInt(string name1, int val1 = default) : base(name1) => Value = val1;
        public override void Write()
        {
            base.Write();
            if (!Host.IsReadOnly)
                Host.Set(Value);
        }
    }
    /// <summary>
    /// ElementId类型的Rvt参数
    /// </summary>
    public class CParamId : CParam
    {
        public ElementId Value { get; set; }
        public CParamId(string name1, ElementId val1 = default) : base(name1) => Value = val1;
        public override void Write()
        {
            base.Write();
            if (!Host.IsReadOnly)
                Host.Set(Value);
        }
    }
    /// <summary>
    /// String类型的Rvt参数
    /// </summary>
    public class CParamStr : CParam
    {
        public string Value { get; set; }
        public CParamStr(string name1, string val1 = default) : base(name1) => Value = val1;
        public override void Write()
        {
            base.Write();
            if (!Host.IsReadOnly)
                Host.Set(Value);
        }
    }
    /// <summary>
    /// Double类型的Rvt参数
    /// </summary>
    public class CParamDbl : CParam
    {
        readonly bool m_ConvertUnit;
        /// <summary>
        /// 是否进行单位转换，比如弧度转角度、英寸转毫米
        /// </summary>
        public bool ConvertUnit => m_ConvertUnit;
        public double Value { get; set; }
        /// <summary>
        /// 构建double类型的参数
        /// </summary>
        /// <param name="convertUnit1">对Element读写参数时是否需要转换单位</param>
        public CParamDbl(string name1, double val1 = default, bool convertUnit1 = true) : base(name1)
        {
            Value = val1;
            m_ConvertUnit = convertUnit1;
        }
        public override void Write()
        {
            base.Write();
            if (!Host.IsReadOnly)
            {
                if (m_ConvertUnit)
                    switch (Host.Definition.ParameterType)
                    {
                        case ParameterType.Angle:
                            Host.Set(Value * Math.PI / 180);
                            return;
                        case ParameterType.Length:
                            Host.Set(Value / 304.8);
                            return;
                        case ParameterType.Area:
                            Host.Set(Value / 304.8 / 304.8);
                            return;
                        case ParameterType.Volume:
                            Host.Set(Value / 304.8 / 304.8 / 304.8);
                            return;
                    }
                Host.Set(Value);
            }
        }
    }
    public class CRec3d
    {
        public CRec3d(XYZ pt1, XYZ pt2, XYZ pt3)
        {
            DirX = (pt2 - pt1).Normalize();
            MmLenX = Math.Round(pt1.DistanceTo(pt2) * 304.8);
            DirY = pt3 - pt3.ProjectTo(pt1, DirX);
            MmLenY = Math.Round(DirY.GetLength() * 304.8);
            DirY = DirY.Normalize();
            Pt0 = pt1;
            DirZ = DirX.CrossProduct(DirY);
        }
        public CRec3d(IEnumerable<XYZ> pts) : this(pts.ElementAt(0), pts.ElementAt(1), pts.ElementAt(2)) { }
        /// <summary>
        /// 世界坐标系原点，可用于族定义时的原始信息再Transform
        /// </summary>
        public CRec3d(double mmLenX, double mmLenY) { MmLenX = mmLenX; MmLenY = mmLenY; }
        public CRec3d(CRec3d a)
        {
            DirZ = a.DirZ;
            DirX = a.DirX;
            DirY = a.DirY;
            MmLenX = a.MmLenX;
            MmLenY = a.MmLenY;
            Pt0 = a.Pt0;
        }
        public XYZ DirX { get; protected set; } = XYZ.BasisX;
        public XYZ DirY { get; protected set; } = XYZ.BasisY;
        public XYZ DirZ { get; protected set; } = XYZ.BasisZ;
        public double MmLenX { get; set; }
        public double MmLenY { get; set; }
        public XYZ Pt0 { get; set; } = XYZ.Zero;
        public XYZ Pt1 => Pt0 + DirX * MmLenX / 304.8;
        public XYZ Pt2 => Pt1 + DirY * MmLenY / 304.8;
        public XYZ Pt3 => Pt0 + DirY * MmLenY / 304.8;
        public virtual void TransformBy(Transform trans1)
        {
            DirZ = trans1.OfVector(DirZ);
            DirX = trans1.OfVector(DirX);
            DirY = trans1.OfVector(DirY);
            Pt0 = trans1.OfPoint(Pt0);
        }
        /// <summary>
        /// 调转方向，Pt0保持不变
        /// </summary>
        public virtual void Reverse()
        {
            DirZ = -DirZ;
            var a = DirX;
            DirX = DirY;
            DirY = a;
            var b = MmLenX;
            MmLenX = MmLenY;
            MmLenY = b;
        }
        /// <summary>
        /// 向前推进，循环旋转
        /// </summary>
        /// <param name="repeatCount">可以是负数</param>
        public virtual void Advance(int repeatCount = 1)
        {
            while (repeatCount < 0) repeatCount += 4;
            repeatCount %= 4;
            while (repeatCount > 0)
            {
                repeatCount--;
                Pt0 = Pt1;
                var a = DirX;
                DirX = DirY;
                DirY = -a;//注意要反向
                var b = MmLenX;
                MmLenX = MmLenY;
                MmLenY = b;
            }
        }
        /// <summary>
        /// 四周向外偏移扩大范围
        /// </summary>
        public virtual void Expand(double mmAdd)
        {
            if (MmLenX > 0)
            {
                MmLenX += mmAdd * 2;
                Pt0 -= DirX * mmAdd / 304.8;
            }
            else
            {
                MmLenX -= mmAdd * 2;
                Pt0 += DirX * mmAdd / 304.8;
            }
            if (MmLenY > 0)
            {
                MmLenY += mmAdd * 2;
                Pt0 -= DirY * mmAdd / 304.8;
            }
            else
            {
                MmLenY -= mmAdd * 2;
                Pt0 += DirY * mmAdd / 304.8;
            }
        }
        //
    }
    public class CSide : CLink2d
    {
        readonly XYZ m_AppendOffsetDir;
        private XYZ m_PtA, m_PtB;
        private double m_ParaA, m_ParaB;
        /// <summary>
        /// UnboundLine
        /// </summary>
        public Line XLine { get; protected set; }
        /// <summary>
        /// 向面积扩大的趋势偏移的法向量
        /// </summary>
        public XYZ AppendOffsetDir => m_AppendOffsetDir;
        public int MmLength => (m_ParaB - m_ParaA).ToMmLength();
        public CSide(XYZ faceNormal, XYZ pt1, XYZ pt2)
        {
            m_PtA = pt1;
            m_PtB = pt2;
            m_ParaA = 0;
            m_ParaB = pt1.DistanceTo(pt2);
            XLine = Line.CreateUnbound(pt1, pt2 - pt1);
            m_AppendOffsetDir = XLine.Direction.CrossProduct(faceNormal);
        }
        public CSide(XYZ faceNormal, Curve l1) : this(faceNormal, l1.GetEndPoint(0), l1.GetEndPoint(1)) { }
        public XYZ PtA
        {
            get { return m_PtA; }
            set { var pj1 = XLine.Project(value); m_PtA = pj1.XYZPoint; m_ParaA = pj1.Parameter; }
        }
        public XYZ PtB
        {
            get { return m_PtB; }
            set { var pj1 = XLine.Project(value); m_PtB = pj1.XYZPoint; m_ParaB = pj1.Parameter; }
        }
        public double ParaA
        {
            get { return m_ParaA; }
            set { m_ParaA = value; m_PtA = XLine.Evaluate(value, false); }
        }
        public double ParaB
        {
            get { return m_ParaB; }
            set { m_ParaB = value; m_PtB = XLine.Evaluate(value, false); }
        }
        public int AngleA { get; set; }
        public int AngleB { get; set; }
        public override void Dispose()
        {
            base.Dispose();
            XLine.Dispose();
        }
    }
}
