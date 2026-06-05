(vl-load-com)

(setq _ct-dir
  (cond
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
        ((wcmatch (strcase _ct-prog) "*ACAD*") "acad")
        ((wcmatch (strcase _ct-prog) "*ZWCAD*") "zwcad")
        (T "gcad")
      )
    )
    (if _ct-plat
      (progn
        (setq _ct-dll (strcat _ct-dir "\\" _ct-plat "\\CadToolkit.dll"))
        (if (vl-file-systime _ct-dll)
          (progn
            (vl-cmdf "NETLOAD" _ct-dll)
            (princ (strcat "\n[CadToolkit] loaded from " _ct-dll))
          )
          (princ (strcat "\n[CadToolkit] Not found: " _ct-dll))
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