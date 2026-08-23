using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;

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
            new ArrangeTestRunner().RunArrangeTest(ArrangeAlgorithmType.Greedy, "Greedy");
        }

        [CommandMethod("T1_BoundedBacktracking")]
        public static void RunArrangeTestBacktracking()
        {
            new ArrangeTestRunner().RunArrangeTest(ArrangeAlgorithmType.BoundedBacktracking, "Bounded Backtracking");
        }

        [CommandMethod("T1_SimulatedAnnealing")]
        public static void RunArrangeTestSimulatedAnnealing()
        {
            new ArrangeTestRunner().RunArrangeTest(ArrangeAlgorithmType.SimulatedAnnealing, "Simulated Annealing");
        }

        [CommandMethod("T1_ForceDirected")]
        public static void RunArrangeTestForceDirected()
        {
            new ArrangeTestRunner().RunArrangeTest(ArrangeAlgorithmType.ForceDirected, "Force Directed");
        }

        [CommandMethod("T1_ConstraintSatisfaction")]
        public static void RunArrangeTestCSP()
        {
            new ArrangeTestRunner().RunArrangeTest(ArrangeAlgorithmType.ConstraintSatisfaction, "Constraint Satisfaction");
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
