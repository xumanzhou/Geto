using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geto
{
    public static class CGeto
    {
        /// <summary>
        /// 当前系统的名称，可作为菜单名或弹窗标题
        /// </summary>
        public static string SysName { get; } = "Geto";
        /// <summary>
        /// 当前系统所在文件夹。最后一个字符是‘\\’
        /// </summary>
        public static string SysPath { get; } = @"C:\Program Files (x86)\Geto\Geto2016\";
        /// <summary>
        /// XYZ.DotProduct或Math.Cos值大于该值时认为是同向
        /// </summary>
        public static double ToleranceCosRadian { get; set; } = Math.Cos(1 * Math.PI / 180);
        /// <summary>
        /// 获取相同环内相邻的前面的元素。需要预先设置前后连接关系
        /// </summary>
        public static T GetPrevious<T>(this T b) where T : CLink2d => b.m_Previous as T;
        /// <summary>
        /// 获取相同环内相邻的后面的元素。需要预先设置前后连接关系
        /// </summary>
        public static T GetNext<T>(this T b) where T : CLink2d => b.m_Next as T;
        /// <summary>
        /// 获取重叠反向边。两个Face相交于一个Edge，这两个Face的Curve重叠反向
        /// </summary>
        public static T GetNegate<T>(this T t1) where T : CLink3d => t1.m_Negate as T;
        /// <summary>
        /// 获取所在圈。需要预先设置前后连接关系。如果是闭合圈则自己在最前
        /// </summary>
        public static LinkedList<T> GetLoop<T>(this T a) where T : CLink2d
        {
            var rt1 = new LinkedList<T>();
            rt1.AddLast(a);
            var b = a.GetNext();
            while (b != null && b != a)
            {
                rt1.AddLast(b);
                b = b.GetNext();
            }
            if (b == null)//非闭环
            {
                b = a.GetPrevious();
                while (b != null)
                {
                    rt1.AddFirst(b);
                    b = b.GetPrevious();
                }
            }
            return rt1;
        }
        /// <summary>
        /// 所有边打断后重新组成多个圈。需要预先设置前后连接关系
        /// </summary>
        public static LinkedList<LinkedList<T>> GetLoops<T>(this IEnumerable<T> co1) where T : CLink2d
        {
            var rt1 = new LinkedList<LinkedList<T>>();
            foreach (var a in co1)
            {
                var hasDo = false;
                foreach (var loop1 in rt1)
                {
                    foreach (var b in loop1)
                        if (hasDo = a == b)
                            break;
                    if (hasDo) break;
                }
                if (!hasDo) rt1.AddLast(a.GetLoop());
            }
            return rt1;
        }
        /// <summary>
        /// 排好前后顺序后设置前后连接
        /// </summary>
        public static void ConnectLoop<T>(this IEnumerable<T> co1) where T : CLink2d
        {
            var a = co1.Last();
            foreach (var b in co1)
            {
                a.SetNext(b);
                b.SetPrevious(a);
                a = b;
            }
        }
        /// <summary>
        /// 移除列表中的无效元素
        /// </summary>
        public static void ClearInvalid<T>(this LinkedList<T> co1) where T : CErr
        {
            var a = co1.Last;
            while (a != null)
            {
                var b = a;
                a = a.Previous;
                if (b.Value.Err != null || b.Value.Code != 0)
                    co1.Remove(b);
            }
        }
        /// <summary>
        /// 检测类实例是否属于某些类别
        /// </summary>
        public static bool IsKindOf(this object elem1, params Type[] types) => types.Contains(elem1.GetType());
        /// <summary>
        /// 弧度转角度并取整
        /// </summary>
        public static int ToAngle(this double radian, int baseAng = 1) => Convert.ToInt32(radian * 180 / Math.PI / baseAng) * baseAng % 360;
        /// <summary>
        /// 角度转弧度
        /// </summary>
        public static double ToRadian(this double angle) => angle * Math.PI / 180;
    }
    /// <summary>
    /// 记录自定义错误信息
    /// </summary>
    public abstract class CErr : IDisposable
    {
        /// <summary>
        /// 具体的错误信息
        /// </summary>
        public Exception Err { get; set; }
        /// <summary>
        /// 值为0时表示正常，值为其它时表示各种自定义错误
        /// </summary>
        public int Code { get; set; }
        public virtual void Dispose()
        {
            Err = null;
            Code = 0;
        }
    }
    /// <summary>
    /// 记录相邻关系。比如同一个Loop里的多个Side，每个Side的前面和后面分别是谁
    /// </summary>
    public abstract class CLink2d : CErr
    {
        internal CLink2d m_Previous, m_Next;//Set用多态，Get用泛型减少类型转换
        public virtual void SetPrevious(CLink2d a) => m_Previous = a;
        public virtual void SetNext(CLink2d b) => m_Next = b;
        /// <summary>
        /// 切换前后连接关系，前变后，后变前
        /// </summary>
        public virtual void ReverseConnect()
        {
            var a = m_Previous;
            m_Previous = m_Next;
            m_Next = a;
        }
        public override void Dispose()
        {
            base.Dispose();
            m_Previous = m_Next = null;
        }
    }
    /// <summary>
    /// 两个Face相交于一个Edge，这两个Face的Curve重叠反向。通过该类可以从一个Face跳转到共Edge的另一个面
    /// </summary>
    public abstract class CLink3d : CLink2d
    {
        internal CLink3d m_Negate;
        public virtual void SetNegate(CLink3d val1) => m_Negate = val1;
        public override void Dispose()
        {
            base.Dispose();
            m_Negate = null;
        }
    }
}
