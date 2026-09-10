# -*- coding: utf-8 -*-
"""Generates one C# table per language from a single source of truth."""
import io, os, sys

OUT = r"C:\Users\merri\Documents\Cities Skylines mods\Seety\Seety\Localization\Strings"

LOCALES = [
    ("En", "en-US"), ("De", "de-DE"), ("Es", "es-ES"), ("Fr", "fr-FR"),
    ("It", "it-IT"), ("Ja", "ja-JP"), ("Ko", "ko-KR"), ("Pl", "pl-PL"),
    ("PtBr", "pt-BR"), ("Ru", "ru-RU"), ("ZhHans", "zh-HANS"), ("ZhHant", "zh-HANT"),
]

# key -> list of 12 strings, in LOCALES order.
D = {}


def add(key, *values):
    assert len(values) == len(LOCALES), (key, len(values))
    D[key] = list(values)


# ---------------------------------------------------------------- options page
add("option.title",
    "Seety", "Seety", "Seety", "Seety", "Seety", "Seety",
    "Seety", "Seety", "Seety", "Seety", "Seety", "Seety")
add("option.tab.Main",
    "Main", "Allgemein", "General", "Général", "Generale", "メイン",
    "기본", "Główne", "Principal", "Основное", "主要", "主要")
add("option.group.DisplayGroup",
    "Display", "Anzeige", "Visualización", "Affichage", "Visualizzazione", "表示",
    "표시", "Wyświetlanie", "Exibição", "Отображение", "显示", "顯示")
add("option.group.FundsGroup",
    "Funds", "Finanzen", "Fondos", "Fonds", "Fondi", "資金",
    "자금", "Fundusze", "Fundos", "Средства", "资金", "資金")
add("option.label.ShowStrip",
    "Show the strip", "Leiste anzeigen", "Mostrar la barra", "Afficher la barre",
    "Mostra la barra", "バーを表示", "바 표시", "Pokaż pasek",
    "Mostrar a barra", "Показывать панель", "显示状态条", "顯示狀態條")
add("option.desc.ShowStrip",
    "Show or hide the whole bar. Click any entry on it to open the matching in-game info view.",
    "Blendet die gesamte Leiste ein oder aus. Ein Klick auf einen Eintrag öffnet die passende Infoansicht.",
    "Muestra u oculta toda la barra. Haz clic en cualquier lectura para abrir su vista de información.",
    "Affiche ou masque toute la barre. Cliquez sur une lecture pour ouvrir la vue d'information correspondante.",
    "Mostra o nasconde tutta la barra. Clicca una lettura per aprire la relativa vista informativa.",
    "バー全体の表示を切り替えます。項目をクリックすると対応するインフォビューが開きます。",
    "바 전체를 표시하거나 숨깁니다. 항목을 클릭하면 해당 정보 보기가 열립니다.",
    "Pokazuje lub ukrywa cały pasek. Kliknij dowolny odczyt, aby otworzyć powiązany widok informacji.",
    "Mostra ou oculta a barra inteira. Clique em qualquer leitura para abrir a visão de informação correspondente.",
    "Показывает или скрывает всю панель. Нажмите на любой показатель, чтобы открыть соответствующий режим информации.",
    "显示或隐藏整个状态条。点击任意读数可打开对应的信息视图。",
    "顯示或隱藏整個狀態條。點擊任一讀數可開啟對應的資訊檢視。")
add("option.label.HighlightProblems",
    "Highlight problems", "Probleme hervorheben", "Resaltar problemas", "Mettre en évidence les problèmes",
    "Evidenzia i problemi", "問題を強調表示", "문제 강조", "Podświetlaj problemy",
    "Destacar problemas", "Выделять проблемы", "突出显示问题", "突顯問題")
add("option.desc.HighlightProblems",
    "Colour an entry amber or red when it crosses a sensible limit. The numbers themselves never change.",
    "Färbt einen Eintrag gelb oder rot, wenn er eine sinnvolle Grenze überschreitet. Die Zahlen selbst ändern sich nie.",
    "Colorea una lectura en ámbar o rojo cuando cruza un límite razonable. Los números nunca cambian.",
    "Colore une lecture en orange ou en rouge lorsqu'elle dépasse une limite raisonnable. Les chiffres ne changent jamais.",
    "Colora una lettura di ambra o rosso quando supera un limite ragionevole. I numeri non cambiano mai.",
    "妥当な限度を超えた項目を黄色や赤で表示します。数値そのものは変わりません。",
    "적절한 한계를 넘으면 항목을 주황색이나 빨간색으로 표시합니다. 수치 자체는 바뀌지 않습니다.",
    "Koloruje odczyt na bursztynowo lub czerwono po przekroczeniu rozsądnej granicy. Same liczby nigdy się nie zmieniają.",
    "Colore uma leitura em âmbar ou vermelho quando ela cruza um limite razoável. Os números nunca mudam.",
    "Окрашивает показатель в жёлтый или красный при выходе за разумный предел. Сами числа не меняются.",
    "当读数越过合理界限时将其标为琥珀色或红色。数字本身永远不变。",
    "當讀數越過合理界限時將其標為琥珀色或紅色。數字本身永遠不變。")
add("option.label.FundsAmount",
    "Amount", "Betrag", "Cantidad", "Montant", "Importo", "金額",
    "금액", "Kwota", "Quantia", "Сумма", "金额", "金額")
add("option.desc.FundsAmount",
    "How much to add to the city treasury. A negative number takes money away instead.",
    "Wie viel der Stadtkasse hinzugefügt wird. Ein negativer Wert nimmt stattdessen Geld weg.",
    "Cuánto añadir a la tesorería de la ciudad. Un número negativo retira dinero.",
    "Montant à ajouter au trésor de la ville. Un nombre négatif retire de l'argent.",
    "Quanto aggiungere alle casse della città. Un numero negativo toglie denaro.",
    "市の財政に加える金額です。負の数を入れると差し引かれます。",
    "도시 재정에 더할 금액입니다. 음수를 넣으면 대신 차감됩니다.",
    "Ile dodać do skarbca miasta. Liczba ujemna zamiast tego odejmuje pieniądze.",
    "Quanto adicionar ao tesouro da cidade. Um número negativo retira dinheiro.",
    "Сколько добавить в казну города. Отрицательное число, наоборот, снимает деньги.",
    "向城市金库增加的金额。负数则会扣除资金。",
    "向城市金庫增加的金額。負數則會扣除資金。")
add("option.label.AddFunds",
    "Apply to treasury", "Auf Stadtkasse anwenden", "Aplicar a la tesorería", "Appliquer au trésor",
    "Applica alle casse", "財政に反映", "재정에 적용", "Zastosuj do skarbca",
    "Aplicar ao tesouro", "Применить к казне", "应用到金库", "套用到金庫")
add("option.desc.AddFunds",
    "This is the one control in Seety that changes your city rather than reporting on it.",
    "Dies ist das einzige Element in Seety, das deine Stadt verändert statt nur über sie zu berichten.",
    "Este es el único control de Seety que cambia tu ciudad en lugar de informar sobre ella.",
    "C'est la seule commande de Seety qui modifie votre ville au lieu d'en rendre compte.",
    "È l'unico controllo di Seety che modifica la città invece di riferire su di essa.",
    "Seety の中で唯一、都市を報告するのではなく変更する操作です。",
    "Seety에서 도시를 보고하는 대신 변경하는 유일한 조작입니다.",
    "To jedyny element Seety, który zmienia miasto, zamiast tylko o nim informować.",
    "Este é o único controle do Seety que altera sua cidade em vez de apenas relatá-la.",
    "Это единственный элемент Seety, который изменяет город, а не сообщает о нём.",
    "这是 Seety 中唯一会改变城市而非报告城市的控件。",
    "這是 Seety 中唯一會改變城市而非報告城市的控制項。")

io.open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "_stage1_ok"), "w").write("ok")
print("stage 1:", len(D), "chiavi")
