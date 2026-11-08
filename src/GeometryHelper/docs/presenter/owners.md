# Owners

Some entities in a drawing are not the user's: a model drawing wrote them from a model, and will write them again when the
model changes. The owner contract is how the presenter keeps its hands off those entities, and how an edit of one of them
still goes through, to the code that owns it, in the same undo step. The presenter knows no owner by name: it knows the
contract, and a model drawing is one owner of it.

## The tag

An owned entity carries a [`CadOwnerTag`](xref:GeometryHelper.Dxf.Viewer.CadOwnerTag) in its extended data under the
application GH_OWNER: the owner's name, the id of the object the entity draws, and where the owner wants one a second id,
such as the view it is in.

```csharp
new CadOwnerTag("Acme.Labels", "A-1", "V-1").WriteTo(line);
// line.XData: 1001 GH_OWNER, 1000 Acme.Labels, 1000 A-1, 1000 V-1
CadOwnerTag.TryRead(line, out CadOwnerTag tag);       // tag.Owner, tag.ObjectId, tag.ExtraId
CadOwnerTag.Remove(line);                             // the data of other applications kept
```

The application is registered in the drawing's table of applications when the tag is written, and AutoCAD keeps the data and
ignores it, so a tagged drawing opens there as any other. Each text is written so that it reads back exactly as given: a
per cent sign, a backslash, a line break or another control character as `%` and two hexadecimal digits, half of a lone
surrogate pair as `%u` and four, and every other letter as it is. Each text, so written, is at most 255 characters, as
extended data holds one; a longer one is refused when the tag is made.

## The rule

An owner says what the presenter may do to each of its entities, as a [`CadEditRule`](xref:GeometryHelper.Dxf.Viewer.CadEditRule):

| Rule | The presenter may | Example in a model drawing |
|---|---|---|
| `Free` | everything, asking nobody: an entity with no tag, or the presenter's own | a line drawn by hand on the sheet |
| `ReadOnly` | nothing, erasing included | a projected line: the model, changed by changing the view |
| `MoveOnly` | move it, and the move goes to the owner | a mark dragged off its part, a view moved on the sheet |
| `Edit` | any edit, each taken or refused by the owner | a dimension moved, its text overridden, erased |

An owner is an [`ICadEntityOwner`](xref:GeometryHelper.Dxf.Viewer.ICadEntityOwner): its `Name`, `GetEditRule(entity)`, and
`TryApply(document, edit)`, which takes an edit, records it and changes its entities in the document, or refuses it. Owners
are registered with the document's [`CadOwnerRegistry`](xref:GeometryHelper.Dxf.Viewer.CadOwnerRegistry), `document.Owners`,
which finds the owner an entity's tag names:

| The entity | Its rule |
|---|---|
| has no tag | `Free` |
| has the tag of a registered owner | what that owner answers |
| has the tag of an owner not registered | `ReadOnly`: nobody could record the edit, and the owner's next update would write over it |
| has the tag of an owner that throws | `ReadOnly`, and the exception is written as a Warn line of [`GeometryHelperLog`](xref:GeometryHelper.GeometryHelperLog) |

The third row is what a saved model drawing opens as in the plain presenter: a beam drawing of 95 tagged entities, its
projected lines, symbols, marks, dimensions and views, is read-only throughout until a
[`Drawing`](xref:GeometryHelper.Drawing.Drawing) opens the file and registers its owner (see
[Update](../drawing/update.md#showing-a-drawing-in-the-presenter)).

## What an edit is

The owner is handed a [`CadEdit`](xref:GeometryHelper.Dxf.Viewer.CadEdit): the handle, a [`CadEditKind`](xref:GeometryHelper.Dxf.Viewer.CadEditKind),
the property, and the old and new values as text, numbers in the invariant culture, as many digits as bring them back to
the bit, separated by commas.

| Kind | `Property` | New value | Made by |
|---|---|---|---|
| `Property` | the row's label: "Layer", "Color", "Text override" | the text the row shows | Properties |
| `Move` | none | the displacement, `10,-2.5` for 10 right and 2.5 down | MOVE, a grip that only translates |
| `Rotate` | none | base point and angle in radians, `x,y,angle` | ROTATE |
| `Scale` | none | base point and factor, `x,y,factor` | SCALE |
| `Mirror` | none | the two points of the mirror line, `x0,y0,x1,y1` | MIRROR |
| `Grip` | the grip's index, `0`, `1` | where it went, `x,y`; the old value is where it was | a grip that reshapes |
| `Erase` | none | none | ERASE |

A mark of handle 2F moved 10 to the right is `new CadEdit("2F", CadEditKind.Move, newValue: "10,0")`.

## How an edit is routed

[`CadDocument.ApplyEdit`](xref:GeometryHelper.Dxf.Viewer.CadDocument) asks the document's gate about every planned change of an
existing entity before it writes anything. The default gate, [`CadOwnerEditGate`](xref:GeometryHelper.Dxf.Viewer.CadOwnerEditGate),
asks the registry for the entity's rule and refuses a kind the rule does not allow, with the words the command line shows:

```
This line belongs to the model and cannot be changed here.
This text belongs to the model and can only be moved.
```

A change the rule lets through, to an entity whose owner takes its own edits (`MoveOnly` or `Edit`), is handed to the owner
as a `CadEdit` through `CadOwnerRegistry.TryApply`. The owner changes the entity and records its hand edit, and what it does
through `ApplyEdit` while it applies joins the same undo step, so one undo takes both back. The action of the presenter's
command then leaves that entity alone ([`CadEditScope`](xref:GeometryHelper.Dxf.Viewer.CadEditScope)`.IsHandled(handle)`), and is not run at all where the owners made every
planned change. An owner that does not take the edit refuses the whole edit, "The model owner did not take this edit.", and
the drawing is read back as it was. An owner that throws has its edit refused and a Warn written.

A copy of an owned entity is the presenter's own: COPY removes the tag from the copy, so it is `Free`, and an update of the
owner does not take it out. A custom gate can be set with `document.EditGate`, and null puts the owners' gate back.

## A small owner

An owner of text labels that lets them be moved and nothing else:

```csharp
public sealed class LabelOwner : ICadEntityOwner
{
    public string Name => "Acme.Labels";

    public CadEditRule GetEditRule(DxfEntity entity) => entity is DxfDBText ? CadEditRule.MoveOnly : CadEditRule.ReadOnly;

    public bool TryApply(CadDocument document, CadEdit edit)
    {
        string[] d = edit.NewValue.Split(',');
        var shift = GeoTransform3.Translation(new GeoVector3(
            double.Parse(d[0], CultureInfo.InvariantCulture), double.Parse(d[1], CultureInfo.InvariantCulture), 0));
        CadEditResult result = document.ApplyEdit("LABEL MOVE",
            new[] { new CadEditRecord(CadEditRecordKind.Transformed, edit.Handle, "Move", null, edit.NewValue) },
            scope => scope.Document.FindEntity(edit.Handle).TransformBy(shift));
        return result.Succeeded;                       // a real owner also records the hand edit, in its own data
    }
}
```

With a LINE and a TEXT tagged "Acme.Labels", and the owner registered with `document.Owners.Register(new LabelOwner())`:

| Before the owner is registered | After |
|---|---|
| line `ReadOnly`, text `ReadOnly` | line `ReadOnly`, text `MoveOnly` |

| The user | What happens |
|---|---|
| MOVE the text by (0, 25) | the owner is handed `Move: (none) -> 0,25`; the text goes from (10, 10) to (10, 35); one undo step `MOVE` with one record |
| ROTATE the text | `This text belongs to the model and can only be moved.` |
| ERASE the line | `This line belongs to the model and cannot be changed here.` |
| COPY the text | two texts, one tagged: the copy is free |

`Register` throws for a null owner, an empty name, or a name registered already; names are compared character by character,
case and all. `Unregister(name)` takes the owner out, and its entities are `ReadOnly` from then on.

## What the model drawing makes of each

[`DrawingOwner`](xref:GeometryHelper.Drawing.DrawingOwner) is the owner of a model drawing's entities, named
`GeometryHelper.Drawing`. It reads the id in the tag and says: a mark or a view (its viewport or its title) `MoveOnly`, a
dimension `Edit` (moved, its text overridden, erased), and everything else `ReadOnly`: the projected lines, a section's
hatch, the symbols, the frame, the title block and the part list. An edit it takes is recorded as a hand edit of the drawing,
and kept through the next update. See [The document in the DXF, and its owner](../drawing/document.md).

## See also

- [Editing as AutoCAD does](editing.md): `ApplyEdit`, the gate and the undo steps the owner joins.
- [Update](../drawing/update.md): how a model drawing keeps hand edits through an update.
- [Command plugins](plugins.md): a plugin's edits go through the same gate.
