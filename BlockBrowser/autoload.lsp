;; BlockBrowser Auto-Loader
;; GstarCAD / AutoCAD / ZWCAD

(vl-load-com)

;; ????????¦Ë????????
(defun _bb-find (/ dir candidates)
  (setq candidates
    (list
      ;; ?????????
      (strcat (getvar "DWGPREFIX") "BlockBrowser")
      ;; ???????¡¤??
      "C:\\BlockBrowser"
      "D:\\BlockBrowser"
      "C:\\mini??????\\BlockBrowser"
      "D:\\mini??????\\BlockBrowser"
      ;; ????
      (strcat (getvar "LOCALROOTPREFIX") "Desktop\\BlockBrowser")
    )
  )
  (setq dir nil)
  (foreach c candidates
    (if (and c (not dir) (vl-file-directory-p c))
      (setq dir c)
    )
  )
  dir
)

(setq blockbrowser-dir (_bb-find))
(setq blockbrowser-loaded nil)

(defun c:BB (/ plat dll)
  (if (null blockbrowser-dir)
    (princ "\n[BlockBrowser] ¦Ä???????????? BlockBrowser ????§Ù??? C:\\ ?? D:\\ ??????")
    (progn
      (if (not blockbrowser-loaded)
        (progn
          (setq plat
            (cond
              ((wcmatch (strcase (getvar "PROGRAM")) "*ACAD*") "acad")
              ((wcmatch (strcase (getvar "PROGRAM")) "*ZWCAD*") "zwcad")
              (T "gcad")
            )
          )
          (setq dll (strcat blockbrowser-dir "\\" plat "\\BlockBrowser.dll"))
          (if (vl-file-systime dll)
            (progn
              (vl-cmdf "NETLOAD" dll)
              (setq blockbrowser-loaded T)
            )
            (princ (strcat "\n[BlockBrowser] ¦Ä??? DLL: " dll))
          )
        )
      )
      (if blockbrowser-loaded (command "BB"))
    )
  )
  (princ)
)

(defun c:KLLQ () (c:BB))

(princ "\nBlockBrowser v1.23 ??????????? BB ?????")
(princ)