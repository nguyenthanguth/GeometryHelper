using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.ArrangeAlgorithms.CadTest
{
    /// <summary>
    /// Registers static CommandMethod commands with AutoCAD.
    /// </summary>
    public static class ArrangeCommands
    {
        [CommandMethod("T1_Greedy")]
        public static void RunArrangeTestGreedy()
        {
            new ArrangeTestRunner().RunArrangeTest("Greedy");
        }

        [CommandMethod("T1_Split")]
        public static void RunSplitTest()
        {
            new SplitTestRunner().RunSplitTest();
        }

        [CommandMethod("T1_ClosestPoint")]
        public static void RunClosestPointTest()
        {
            new ClosestPointTestRunner().RunClosestPointTest();
        }

        [CommandMethod("T1_RectangleCombine")]
        public static void RunRectangleCombineTest()
        {
            new RectangleCombineTestRunner().RunRectangleCombineTest();
        }

        [CommandMethod("T1_Join")]
        public static void RunJoinTest()
        {
            new JoinTestRunner().RunJoinTest();
        }

        [CommandMethod("T1_JoinBackup")]
        public static void RunJoinBackupTest()
        {
            new JoinTestRunner().RunJoinBackupTest();
        }

        [CommandMethod("T1_SplitAutoTest")]
        public static void RunSplitAutoTest()
        {
            SplitAutoTestRunner.RunSplitAutoTest();
        }
    }
}
