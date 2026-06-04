;; BlockBrowser Auto-Loader
;; GstarCAD / AutoCAD / ZWCAD

(vl-load-com)

;; 搜索常见位置找插件目录
(defun _bb-find (/ dir candidates)
  (setq candidates
    (list
      ;; 相对当前图纸目录
      (strcat (getvar "DWGPREFIX") "BlockBrowser")
      ;; 常见固定路径
      "C:\\BlockBrowser"
      "D:\\BlockBrowser"
      "C:\\mini工具箱\\BlockBrowser"
      "D:\\mini工具箱\\BlockBrowser"
      ;; 桌面
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
    (princ "\n[BlockBrowser] 未找到插件目录，请将 BlockBrowser 文件夹放在 C:\\ 或 D:\\ 根目录。")
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

(princ "\nBlockBrowser v1.23 已就绪，输入 BB 启动。")
(princ)
