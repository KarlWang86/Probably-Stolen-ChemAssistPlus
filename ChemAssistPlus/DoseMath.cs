using System;

namespace ChemAssistPlus
{
    public static class DoseMath
    {
        public static long ProteinNeed(long protein, long acid, long alkali)
        {
            if (protein < 0 || acid < 0 || alkali < 0) throw new ArgumentOutOfRangeException("Negative chemical amount.");
            return Math.Max(0, protein - Math.Abs(acid - alkali));
        }
        // 纯数学内核，不依赖 Unity。整数微升向上取整，乘法先升为 long 防溢出。
        // Tests/Program.cs 检查真实稀释案例和 20,000 个边界；不代替游戏内测试。
        // All native chemical quantities are integer microlitres. Round the solution up, never the concentration.
        public static long RequiredSolution(int desiredActive, int sourceActive, int sourceTotal)
        {
            if (desiredActive < 0 || sourceActive <= 0 || sourceTotal < sourceActive)
                throw new ArgumentOutOfRangeException("Invalid chemical amount or source concentration.");
            return ((long)desiredActive * sourceTotal + sourceActive - 1L) / sourceActive;
        }
    }
}
