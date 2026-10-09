# Заголовок главного меню

`TitlePrint.png` — цельная надпись PIRATE SLOP, отрисованная в Unity из установленной в Windows Georgia Bold. Добавлена лёгкая процедурная потёртость печати. Это готовое изображение фразы, а не шрифт или набор отдельных глифов. Файлы шрифта не копируются и не включаются в проект.

Microsoft допускает использование готовых PNG-надписей в игре: [Font redistribution FAQ](https://learn.microsoft.com/en-us/typography/fonts/font-faq), раздел «Can I include graphic files … in my game or apps, say for a logo or banner?».

Остальной текст меню использует уже установленную на машине Georgia и Segoe UI через существующий механизм `Font.CreateDynamicFontFromOSFont`. Поддержка платформ без этих системных шрифтов требует отдельного лицензированного шрифта; этот редизайн не добавляет файлы системных шрифтов в сборку.
