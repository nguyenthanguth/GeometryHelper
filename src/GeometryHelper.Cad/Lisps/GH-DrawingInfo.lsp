;;; GH-DrawingInfo.lsp -- a sample routine: GHINFO writes what the drawing holds.
;;;
;;; Type GHINFO at the command line. It only reads; the drawing is not changed. It uses
;;; standard AutoLISP only, so the same file runs in AutoCAD 2021 as well.
;;;
;;; The output for a drawing of two circles and a line, a layer Steel and a block BOLT:
;;;
;;;   Drawing: Plan.dwg in C:\Projects\
;;;   Units: INSUNITS 4, LUNITS 2
;;;   Current layer: Steel, text style: Standard
;;;   Model space: 3 objects
;;;     CIRCLE   2
;;;     LINE     1
;;;   Layers: 2, blocks: 1
;;;   Extents: (-10.00,-10.00) to (120.00,80.00)

(defun c:GHINFO (/ ss i name entry counts layers blocks rec lo hi)
  (princ (strcat "\nDrawing: " (getvar "DWGNAME") " in " (getvar "DWGPREFIX")))
  (princ
    (strcat "\nUnits: INSUNITS " (itoa (getvar "INSUNITS"))
            ", LUNITS " (itoa (getvar "LUNITS"))
    )
  )
  (princ
    (strcat "\nCurrent layer: " (getvar "CLAYER")
            ", text style: " (getvar "TEXTSTYLE")
    )
  )

  ;; Every object of model space, found with a selection of the whole database
  ;; filtered by the layout it lies in (group 410), counted by its type (group 0).
  (setq counts '())
  (if (setq ss (ssget "_X" '((410 . "Model"))))
    (repeat (setq i (sslength ss))
      (setq name  (cdr (assoc 0 (entget (ssname ss (setq i (1- i))))))
            entry (assoc name counts)
      )
      (if entry
        (setq counts (subst (cons name (1+ (cdr entry))) entry counts))
        (setq counts (cons (cons name 1) counts))
      )
    )
  )
  (princ
    (strcat "\nModel space: " (itoa (if ss (sslength ss) 0)) " objects")
  )
  (foreach entry (vl-sort counts '(lambda (a b) (< (car a) (car b))))
    (princ
      (strcat "\n  " (car entry)
              (substr "                    " 1 (max 1 (- 9 (strlen (car entry)))))
              (itoa (cdr entry))
      )
    )
  )

  ;; The symbol tables are read one record at a time with tblnext. A block whose
  ;; name starts with * is a space or an anonymous block, and is not counted.
  (setq layers 0
        rec    (tblnext "LAYER" T)
  )
  (while rec
    (setq layers (1+ layers)
          rec    (tblnext "LAYER")
    )
  )
  (setq blocks 0
        rec    (tblnext "BLOCK" T)
  )
  (while rec
    (if (/= (substr (cdr (assoc 2 rec)) 1 1) "*")
      (setq blocks (1+ blocks))
    )
    (setq rec (tblnext "BLOCK"))
  )
  (princ (strcat "\nLayers: " (itoa layers) ", blocks: " (itoa blocks)))

  ;; The extents are an empty box (the minimum above the maximum) until something is drawn.
  (setq lo (getvar "EXTMIN")
        hi (getvar "EXTMAX")
  )
  (if (> (car lo) (car hi))
    (princ "\nExtents: none")
    (princ
      (strcat "\nExtents: (" (rtos (car lo) 2 2) "," (rtos (cadr lo) 2 2)
              ") to (" (rtos (car hi) 2 2) "," (rtos (cadr hi) 2 2) ")"
      )
    )
  )
  (princ)
)

(princ)
