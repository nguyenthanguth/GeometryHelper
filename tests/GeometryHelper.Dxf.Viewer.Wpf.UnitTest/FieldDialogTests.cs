using System;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Field dialog and the text editor's Insert Field and Edit Field, driven with no window: the dialog's logic over its
    /// request, FIELD making an MTEXT of the field, Insert Field putting a field in the words being edited and a double click on
    /// a field editing it.
    /// </summary>
    public class FieldDialogTests
    {
        private static MainViewModel OpenWithCircle(out DxfCircle circle, out DxfMText mtext)
        {
            var database = new DxfDatabase("AC1032");
            circle = new DxfCircle { Center = new GeoPoint3(100, 100, 0), Radius = 10 };
            database.ModelSpace.AppendEntity(circle);
            mtext = new DxfMText
            {
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 2.5,
                Contents = "Area: ",
            };
            database.ModelSpace.AppendEntity(mtext);
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "plan.dxf"), "plan.dxf");
            return model;
        }

        [Fact]
        public void TheDialogListsTheCategoriesAndNames_ChoosesAField_AndGivesItsExpressionAndPreview()
        {
            MainViewModel model = OpenWithCircle(out _, out _);
            var dialog = new FieldDialogViewModel(model.NewFieldRequest());

            Assert.Equal(
                new[] { "All", "Date & Time", "Document", "Linked", "Objects", "Other", "Plot" },
                dialog.Categories
            );
            Assert.Equal("Date", dialog.SelectedName.Name);
            Assert.True(dialog.IsDate);
            Assert.False(dialog.IsNumber);
            Assert.True(dialog.CanOk);
            Assert.StartsWith("%<\\AcVar Date \\f \"", dialog.Code);
            Assert.Equal(12, dialog.DateChoices.Count);

            dialog.DateFormat = "yyyy";
            Assert.Equal("%<\\AcVar Date \\f \"yyyy\">%", dialog.Code);
            Assert.Equal(DateTime.Now.Year.ToString(CultureInfo.InvariantCulture), dialog.Preview);

            dialog.Category = "Document";
            Assert.Equal("Document", dialog.Category);
            Assert.DoesNotContain(dialog.Names, n => n.Name == "Date");
            dialog.SelectedName = dialog.Names.Single(n => n.Name == "Author");
            Assert.True(dialog.HasFormats);
            Assert.False(dialog.IsDate);
            Assert.Equal("%<\\AcVar Author>%", dialog.Code);
            dialog.SelectedFormat = dialog.Formats.Single(f => f.Label == "Uppercase");
            Assert.Equal("%<\\AcVar Author \\f \"%tc1\">%", dialog.Code);

            dialog.Category = "Other";
            dialog.SelectedName = dialog.Names.Single(n => n.Name == "SystemVariable");
            Assert.True(dialog.IsExpression);
            Assert.Equal("System variable name:", dialog.ExpressionLabel);
            Assert.False(dialog.CanOk);
            dialog.Expression = "CTAB";
            Assert.True(dialog.CanOk);
            Assert.Equal("%<\\AcVar CTAB>%", dialog.Code);
            Assert.Equal("Model", dialog.Preview);

            dialog.SelectedName = dialog.Names.Single(n => n.Name == "DieselExpression");
            Assert.Equal("DIESEL expression:", dialog.ExpressionLabel);
            dialog.Expression = "$(+,1,2)";
            Assert.Equal("3", dialog.Preview);
        }

        [Fact]
        public void AnObjectFieldReadsTheObjectPicked_ItsPropertyAndTheUnitsAndPrecisionAFormatSays()
        {
            MainViewModel model = OpenWithCircle(out DxfCircle circle, out _);
            var dialog = new FieldDialogViewModel(model.NewFieldRequest());
            dialog.Category = "Objects";
            dialog.SelectedName = dialog.Names.Single(n => n.Name == "Object");

            Assert.True(dialog.IsObject);
            Assert.Equal("No object selected", dialog.ObjectText);
            Assert.False(dialog.CanOk);

            dialog.Request.SetObject(circle.Handle);
            Assert.Equal("AcDbCircle " + circle.Handle, dialog.ObjectText);
            Assert.Contains(dialog.PropertyChoices, p => p.Name == "Area");
            dialog.SelectedProperty = dialog.PropertyChoices.Single(p => p.Name == "Area");
            Assert.True(dialog.IsNumber);
            Assert.Equal("314.159265", dialog.Preview);

            dialog.SelectedUnits = dialog.Units.Single(u => u.Label == "Decimal");
            dialog.Precision = "1";
            dialog.Prefix = "A=";
            dialog.Suffix = " mm2";
            Assert.Equal("A=314.2 mm2", dialog.Preview);
            Assert.EndsWith(".Area \\f \"%lu2%pr1%ps[A=, mm2]\">%", dialog.Code);

            dialog.FactorText = "0.01";
            dialog.Suffix = " cm2";
            Assert.Equal("A=3.1 cm2", dialog.Preview);
        }

        [Fact]
        public void FIELD_AsksForTheObjectWhereSelectObjectWasPressed_ThenMakesAnMTextOfTheFieldAtTheStartPoint()
        {
            MainViewModel model = OpenWithCircle(out DxfCircle circle, out _);
            int shown = 0;
            model.ShowField = dialog =>
            {
                shown++;

                if (shown == 1)
                {
                    dialog.Category = "Objects";
                    dialog.SelectedName = dialog.Names.Single(n => n.Name == "Object");
                    dialog.Close(CadFieldAction.SelectObject);
                    return true;
                }

                Assert.Equal(circle.Handle, dialog.Request.ObjectHandle);
                dialog.SelectedProperty = dialog.PropertyChoices.Single(p => p.Name == "Radius");
                dialog.Close(CadFieldAction.Ok);
                return true;
            };

            model.Execute("FIELD");
            model.Session.FeedSelection(new[] { circle.Handle });
            model.Execute("30,40");

            Assert.Equal(2, model.Document.Database.ModelSpace.OfType<DxfMText>().Count());
            Assert.Equal(2, shown);
            DxfMText made = model.Document.Database.ModelSpace.OfType<DxfMText>().Single(m => m.Contents != "Area: ");
            Assert.Equal("10.000000", made.Contents);
            Assert.True(CadFields.Has(made));
            Assert.Equal(30.0, made.Location.X, 6);
            Assert.Equal(40.0, made.Location.Y, 6);
            Assert.Equal(2, shown);
            Assert.Equal("FIELD", model.Document.NextUndo.Name);
        }

        [Fact]
        public void InsertFieldInTheMTextEditor_PutsTheFieldAtTheCaretAsOnePlace_AndClosingWritesItEvaluated()
        {
            MainViewModel model = OpenWithCircle(out DxfCircle circle, out DxfMText mtext);
            model.Document.Selection.Set(new[] { circle.Handle });
            int shown = 0;
            model.ShowField = dialog =>
            {
                shown++;

                if (shown == 1)
                {
                    dialog.Category = "Objects";
                    dialog.SelectedName = dialog.Names.Single(n => n.Name == "Object");
                    dialog.Close(CadFieldAction.SelectObject);
                    return true;
                }

                dialog.SelectedProperty = dialog.PropertyChoices.Single(p => p.Name == "Area");
                dialog.SelectedUnits = dialog.Units.Single(u => u.Label == "Decimal");
                dialog.Precision = "2";
                dialog.Close(CadFieldAction.Ok);
                return true;
            };

            Assert.True(model.EditTextAt(mtext.Handle));
            TextEditorViewModel editor = model.TextEditor;
            editor.Editor.MoveTo(editor.Editor.Length);
            editor.FieldCommand.Execute(null);

            Assert.Equal(2, shown);
            Assert.True(editor.Editor.HasFields);
            Assert.Equal("Area: 314.16", editor.Editor.Content.PlainText);
            Assert.StartsWith("Area: %<\\AcObjProp Object(%<\\_ObjId ", editor.Editor.Contents);

            editor.CloseCommand.Execute(null);

            DxfMText edited = model.Document.Database.ModelSpace.OfType<DxfMText>().Single();
            Assert.Equal("Area: 314.16", edited.Contents);
            Assert.True(CadFields.Has(edited));
        }

        [Fact]
        public void ADoubleClickOnAFieldInTheEditor_ShowsTheDialogLoadedWithTheField_AndTheFieldItEndsWithTakesItsPlace()
        {
            MainViewModel model = OpenWithCircle(out _, out DxfMText mtext);
            var context = new CadFieldContext(model.Document) { Culture = CultureInfo.InvariantCulture };
            CadFields.Set(mtext, "Year %<\\AcVar Date \\f \"yyyy\">% end", context);
            Assert.True(model.EditTextAt(mtext.Handle));
            TextEditorViewModel editor = model.TextEditor;
            Assert.Equal("Year %<\\AcVar Date \\f \"yyyy\">% end", editor.Edit.Value);
            Assert.Equal(
                DateTime.Now.Year.ToString(CultureInfo.InvariantCulture),
                Assert.Single(editor.Edit.FieldShown)
            );
            model.ShowField = dialog =>
            {
                Assert.Equal("Date", dialog.SelectedName.Name);
                Assert.Equal("yyyy", dialog.DateFormat);
                dialog.DateFormat = "MMM";
                dialog.Close(CadFieldAction.Ok);
                return true;
            };

            Assert.False(editor.EditFieldAt(0));
            Assert.True(editor.EditFieldAt(5));

            Assert.Equal("Year %<\\AcVar Date \\f \"MMM\">% end", editor.Editor.Contents);
            editor.CloseCommand.Execute(null);
            Assert.Equal(
                "Year " + DateTime.Now.ToString("MMM", CultureInfo.CurrentCulture) + " end",
                model.Document.Database.ModelSpace.OfType<DxfMText>().Single().Contents
            );
        }

        [Fact]
        public void WithNoWindowToShowTheDialog_FIELDSaysSoAndMakesNothing()
        {
            MainViewModel model = OpenWithCircle(out _, out _);
            model.ShowField = null;

            model.Execute("FIELD");

            Assert.Single(model.Document.Database.ModelSpace.OfType<DxfMText>());
        }

        [Fact]
        public void TheEditorsRightClick_OffersInsertField_AndAtAFieldEditFieldAndConvertFieldToText()
        {
            MainViewModel model = OpenWithCircle(out _, out DxfMText mtext);
            var context = new CadFieldContext(model.Document) { Culture = CultureInfo.InvariantCulture };
            CadFields.Set(mtext, "Year %<\\AcVar Date \\f \"yyyy\">% end", context);
            Assert.True(model.EditTextAt(mtext.Handle));
            TextEditorViewModel editor = model.TextEditor;

            editor.Editor.MoveTo(0);
            Assert.Equal("Insert Field…    Ctrl+F", editor.Menu("Editor")[0].Header);
            Assert.DoesNotContain(editor.Menu("Editor"), m => m.Header == "Edit Field…");

            editor.Editor.MoveTo(6);
            string[] headers = editor.Menu("Editor").Select(m => m.Header).ToArray();
            Assert.Equal("Insert Field…    Ctrl+F", headers[0]);
            Assert.Equal("Edit Field…", headers[1]);
            Assert.Equal("Convert Field To Text", headers[2]);

            editor.Menu("Editor").Single(m => m.Header == "Convert Field To Text").Command.Execute(null);

            string year = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);
            Assert.False(editor.Editor.HasFields);
            Assert.Equal("Year " + year + " end", editor.Editor.Contents);
            editor.CloseCommand.Execute(null);
            DxfMText after = model.Document.Database.ModelSpace.OfType<DxfMText>().Single();
            Assert.Equal("Year " + year + " end", after.Contents);
            Assert.False(CadFields.Has(after));
        }
    }
}
