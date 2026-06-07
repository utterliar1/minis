;; BlockBrowser Auto-Loader
;; GstarCAD / AutoCAD / ZWCAD

(vl-load-com)

(defun _bb-getvar (sym / v)
  (setq v (getvar sym))
  (if v v "")
)

(defun _bb-find (/ dir candidates)
  (setq candidates
    (list
      (strcat (_bb-getvar "DWGPREFIX") "BlockBrowser")
      "C:\\BlockBrowser"
      "D:\\BlockBrowser"
      (strcat (_bb-getvar "LOCALROOTPREFIX") "Desktop\\BlockBrowser")
    )
  )
  (setq dir nil)
  (foreach c candidates
    (if (and c (> (strlen c) 0) (not dir) (vl-file-directory-p c))
      (setq dir c)
    )
  )
  dir
)

(defun _bb-run-panel (/ doc)
  (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
  (vla-SendCommand doc "BBPANEL ")
)

(setq blockbrowser-dir (_bb-find))
(setq blockbrowser-loaded nil)

(defun c:BB (/ plat dll)
  (if (null blockbrowser-dir)
    (princ "\n[BlockBrowser] Plugin folder not found. Put BlockBrowser under C:\\ or D:\\.")
    (progn
      (if (not blockbrowser-loaded)
        (progn
          (setq plat
            (cond
              ((wcmatch (strcase (_bb-getvar "PROGRAM")) "*ZWCAD*") "zwcad")
              ((wcmatch (strcase (_bb-getvar "PROGRAM")) "*ACAD*") "acad")
              (T "gcad")
            )
          )
          (setq dll (strcat blockbrowser-dir "\\" plat "\\BlockBrowser.dll"))
          (if (vl-file-systime dll)
            (progn
              (vl-cmdf "NETLOAD" dll)
              (setq blockbrowser-loaded T)
              (_bb-run-panel)
            )
            (princ (strcat "\n[BlockBrowser] DLL not found: " dll))
          )
        )
        (_bb-run-panel)
      )
    )
  )
  (princ)
)

(defun c:KLLQ () (c:BB))

(princ "\nBlockBrowser v1.25.2 ready. Type BB to start.")
(princ)
