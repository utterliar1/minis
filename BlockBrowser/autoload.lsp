;; BlockBrowser Auto-Loader
;; 浩辰CAD / AutoCAD / 中望CAD

(vl-load-com)

;; 读取同目录config.ini的值
(defun _bb-config-val (key / cfg f line pos k val)
  (setq val nil)
  (setq cfg (strcat blockbrowser-dir "config.ini"))
  (if (findfile cfg)
    (progn
      (setq f (open cfg "r"))
      (while (setq line (read-line f))
        (if (and (> (strlen line) 0)
                 (not (wcmatch (substr line 1 1) ";"))
                 (not (wcmatch (substr line 1 1) "#")))
          (progn
            (setq pos (vl-string-search "=" line))
            (if pos
              (progn
                (setq k (substr line 1 pos))
                (if (= (strcase k) (strcase key))
                  (setq val (substr line (+ pos 2)))
                )
              )
            )
          )
        )
      )
      (close f)
    )
  )
  val
)

;; 扫描常见位置找插件目录
(defun _bb-find (/ dir)
  (cond
    ((vl-file-directory-p "C:\\BlockBrowser") "C:\\BlockBrowser\\")
    ((vl-file-directory-p "D:\\BlockBrowser") "D:\\BlockBrowser\\")
    ((vl-file-directory-p "C:\\mini工具箱\\BlockBrowser") "C:\\mini工具箱\\BlockBrowser\\")
    ((vl-file-directory-p "D:\\mini工具箱\\BlockBrowser") "D:\\mini工具箱\\BlockBrowser\\")
    (T nil)
  )
)

(setq blockbrowser-dir (_bb-find))

(defun c:BB (/ plat dll)
  (if (null blockbrowser-dir)
    (princ "\n[块浏览器] 未找到插件目录。")
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
      (command "BB")
    )
  )
  (princ)
)

(defun c:KLLQ () (c:BB))

(princ "\n块浏览器已就绪，输入 BB 启动。")
(princ)