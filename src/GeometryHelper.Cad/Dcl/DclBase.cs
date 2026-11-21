namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// The predefined tiles (D306): a DCL text written for this project from what the AutoCAD DCL reference says the standard
    /// tiles are, never Autodesk's <c>base.dcl</c>. <see cref="DclParser"/> reads it once and puts every definition in front of
    /// the definitions of each file it parses, so a dialog can name <c>ok_cancel</c> or <c>edit_box</c> without including
    /// anything and a file can still define a tile of the same name of its own.
    /// </summary>
    internal static class DclBase
    {
        /// <summary>The path the predefined tiles are reported under in a tile's <see cref="DclTile.File"/>.</summary>
        internal const string Path = "(predefined tiles)";

        /// <summary>The text of the predefined tiles.</summary>
        internal const string Text = """
            // The predefined tiles of GeometryHelper.Cad.
            // Written for this project from the description of the standard tiles in the AutoCAD DCL reference.

            // Clusters. A row or a column adds no margin of its own; a boxed one has a frame and a label (a blank one).
            row : cluster { layout = horizontal; horizontal_margin = none; vertical_margin = none; }
            column : cluster { layout = vertical; horizontal_margin = none; vertical_margin = none; }
            boxed_row : cluster { layout = horizontal; boxed = true; label = " "; }
            boxed_column : cluster { layout = vertical; boxed = true; label = " "; }
            radio_row : radio_cluster { layout = horizontal; horizontal_margin = none; vertical_margin = none; }
            radio_column : radio_cluster { layout = vertical; horizontal_margin = none; vertical_margin = none; }
            boxed_radio_row : radio_cluster { layout = horizontal; boxed = true; label = " "; }
            boxed_radio_column : radio_cluster { layout = vertical; boxed = true; label = " "; }

            // Text set out as one line (concatenation) or as lines under each other (paragraph) out of text_part tiles.
            concatenation : row { fixed_width = true; fixed_height = true; }
            paragraph : column { fixed_width = true; fixed_height = true; }
            text_part : text { horizontal_margin = none; vertical_margin = none; }

            // Spacers.
            spacer_0 : spacer { width = 0; height = 0; }
            spacer_1 : spacer { width = 1; height = 1; }

            // Buttons: all eight characters wide and not stretched.
            default_button : button { is_default = true; fixed_width = true; width = 8; alignment = centered; }
            retirement_button : button { fixed_width = true; width = 8; alignment = centered; }
            ok_button : default_button { label = "  OK  "; key = "accept"; is_default = true; }
            cancel_button : retirement_button { label = "Cancel"; key = "cancel"; is_cancel = true; }
            help_button : retirement_button { label = "&Help"; key = "help"; is_help = true; }
            info_button : retirement_button { label = "&Info..."; key = "info"; }

            // The rows of exit buttons, centred, with a gap of two characters between the buttons.
            ok_only : row { fixed_width = true; alignment = centered; : ok_button { is_cancel = true; } }
            ok_cancel : row {
                fixed_width = true; alignment = centered;
                ok_button; : spacer_0 { width = 2; } cancel_button;
            }
            ok_cancel_help : row {
                fixed_width = true; alignment = centered;
                ok_button; : spacer_0 { width = 2; } cancel_button; : spacer_0 { width = 2; } help_button;
            }
            ok_cancel_help_info : row {
                fixed_width = true; alignment = centered;
                ok_button; : spacer_0 { width = 2; } cancel_button; : spacer_0 { width = 2; } help_button;
                : spacer_0 { width = 2; } info_button;
            }

            // The error line and the exit rows that carry one under it.
            errtile : text { key = "error"; width = 35; value = ""; }
            ok_cancel_help_errtile : column { errtile; ok_cancel_help; }
            ok_cancel_err : column { errtile; ok_cancel; }

            // Images and edit boxes of a usual size.
            image_block : image { width = 10; aspect_ratio = 1; }
            icon_image : image { width = 4; aspect_ratio = 1; }
            edit12_box : edit_box { edit_width = 12; }
            edit32_box : edit_box { edit_width = 32; }
            """;
    }
}
