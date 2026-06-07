(vl-load-com)

(defun _ct-root-from-load-dir (dir / base)
  (setq base (strcase (vl-filename-base dir)))
  (if (member base '("ACAD" "ZWCAD" "GCAD"))
    (vl-filename-directory dir)
    dir
  )
)

(setq _ct-dir
  (cond
    ((and (boundp '*load-truename*) *load-truename*)
      (_ct-root-from-load-dir (vl-filename-directory *load-truename*)))
    ((findfile "autoload.lsp")
      (_ct-root-from-load-dir (vl-filename-directory (findfile "autoload.lsp"))))
    ((vl-file-directory-p "C:\\CadToolkit") "C:\\CadToolkit")
    ((vl-file-directory-p "D:\\CadToolkit") "D:\\CadToolkit")
    (T nil)
  )
)

(if _ct-dir
  (progn
    (setq _ct-prog (getvar "PROGRAM"))
    (setq _ct-plat
      (cond
        ((not _ct-prog) "zwcad")
        ((wcmatch (strcase _ct-prog) "*ZWCAD*") "zwcad")
        ((wcmatch (strcase _ct-prog) "*ACAD*") "acad")
        (T "gcad")
      )
    )

    (if _ct-plat
      (progn
        (setq _ct-dll (strcat _ct-dir "\\" _ct-plat "\\CadToolkit.dll"))
        (if (vl-file-systime _ct-dll)
          (progn
            (setvar "CMDECHO" 0)
            (vl-cmdf "NETLOAD" _ct-dll)
            (setvar "CMDECHO" 1)
            (princ "\nCadToolkit v1.23.1 ready. Type CC to start.")
          )
          (princ (strcat "\n[CadToolkit] DLL not found: " _ct-dll))
        )
      )
    )
  )
  (princ "\n[CadToolkit] Not found on C:\\ or D:\\")
)

(defun c:CC (/ ss i f handleStr oldCmdEcho)
  (setq oldCmdEcho (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)
  (setq ss (ssget "_I"))
  (if ss
    (progn
      (setq handleStr "")
      (setq i 0)
      (repeat (sslength ss)
        (if (> i 0) (setq handleStr (strcat handleStr ",")))
        (setq handleStr (strcat handleStr
          (cdr (assoc 5 (entget (ssname ss i))))))
        (setq i (1+ i))
      )
      (setq f (open (strcat _ct-dir "\\pickfirst.txt") "w"))
      (if f
        (progn (write-line handleStr f) (close f))
        (princ "\n[CadToolkit] Cannot write pickfirst.txt")
      )
    )
    (if (findfile (strcat _ct-dir "\\pickfirst.txt"))
      (vl-file-delete (strcat _ct-dir "\\pickfirst.txt"))
    )
  )
  (setvar "CMDECHO" oldCmdEcho)
  (vl-cmdf "CT_PANEL")
  (princ)
)

(defun c:CT () (c:CC))

(princ)
