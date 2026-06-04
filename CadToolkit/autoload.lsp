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
    (setq _ct-dll (strcat _ct-dir "\\" _ct-plat "\\CadToolkit.dll"))
    (if (vl-file-systime _ct-dll)
      (vl-cmdf "NETLOAD" _ct-dll)
      (princ (strcat "\n[CadToolkit] Not found: " _ct-dll))
    )
  )
  (princ "\n[CadToolkit] Not found on C:\\ or D:\\")
)

(princ)