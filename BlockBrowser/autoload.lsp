;; BlockBrowser Auto-Loader
;; 浩辰CAD / AutoCAD / 中望CAD

(vl-load-com)

;; 扫描常见位置找插件目录
(defun _bb-find (/ dir)
  (cond
    ((vl-file-directory-p "C:\\BlockBrowser") "C:\\BlockBrowser\\")
    ((vl-file-directory-p "D:\\BlockBrowser") "D:\\BlockBrowser\\")
    (T nil)
  )
)

(setq blockbrowser-dir (_bb-find))
(setq blockbrowser-loaded nil)

(defun c:BB (/ plat dll)
  (if (null blockbrowser-dir)
    (princ "\n[块浏览器] 未找到插件目录。")
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
          (setq dll (strcat blockbrowser-dir plat "\\BlockBrowser.dll"))
          (vl-cmdf "NETLOAD" dll)
          (setq blockbrowser-loaded T)
        )
      )
      (command "BB")
    )
  )
  (princ)
)

(defun c:KLLQ () (c:BB))

(princ "\n块浏览器 v1.2 已就绪，输入 BB 启动。")
(princ)