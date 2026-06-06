;; BlockBrowser Auto-Loader
;; GstarCAD / AutoCAD / ZWCAD

(vl-load-com)

;; safe getvar - returns "" instead of nil
(defun _bb-getvar (sym / v)
  (setq v (getvar sym))
  (if v v "")
)

;; search common locations for plugin directory
(defun _bb-find (/ dir candidates)
  (setq candidates
    (list
      (strcat (_bb-getvar "DWGPREFIX") "BlockBrowser")
      "C:\\BlockBrowser"
      "D:\\BlockBrowser"
      "C:\\mini工具箱\\BlockBrowser"
      "D:\\mini工具箱\\BlockBrowser"
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

(setq blockbrowser-dir (_bb-find))
(setq blockbrowser-loaded nil)

(defun c:BB (/ plat dll)
  (if (null blockbrowser-dir)
    (princ "\n[BlockBrowser] 未找到插件目录，请将 BlockBrowser 文件夹放在 C:\\ 或 D:\\ 根目录。")
    (progn
      (if (not blockbrowser-loaded)
        (progn
          (setq plat
            (cond
              ((wcmatch (strcase (_bb-getvar "PROGRAM")) "*ACAD*") "acad")
              ((wcmatch (strcase (_bb-getvar "PROGRAM")) "*ZWCAD*") "zwcad")
              (T "gcad")
            )
          )
          (setq dll (strcat blockbrowser-dir "\\" plat "\\BlockBrowser.dll"))
          (if (vl-file-systime dll)
            (progn
              (vl-cmdf "NETLOAD" dll)
              (setq blockbrowser-loaded T)
            )
            (princ (strcat "\n[BlockBrowser] 未找到 DLL: " dll))
          )
        )
      )
      (if blockbrowser-loaded (command "BB"))
    )
  )
  (princ)
)

(defun c:KLLQ () (c:BB))

(princ "\nBlockBrowser v1.25 已就绪，输入 BB 启动。")
(princ)
