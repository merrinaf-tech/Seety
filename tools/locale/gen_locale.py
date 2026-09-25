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

add("option.label.ToolbarTrends",
    "Show change on the bottom bar", "Veränderung in der unteren Leiste zeigen",
    "Mostrar la variación en la barra inferior", "Afficher la variation sur la barre du bas",
    "Mostra la variazione nella barra in basso", "下部バーに増減を表示",
    "하단 바에 증감 표시", "Pokaż zmianę na dolnym pasku",
    "Mostrar a variação na barra inferior", "Показывать изменение на нижней панели",
    "在底部栏显示变化", "在底部列顯示變化")
add("option.desc.ToolbarTrends",
    "Prints the change beside the population and money figures on the game's own bottom bar, which otherwise only shows it while you hover. The number is the game's own, not one Seety works out. This is the only thing Seety draws outside its own bar, so turn it off if a game update ever makes it look wrong.",
    "Zeigt die Veränderung neben Bevölkerung und Geld in der unteren Leiste des Spiels an, die sie sonst nur beim Daraufzeigen einblendet. Die Zahl stammt vom Spiel, nicht von Seety. Das ist das Einzige, was Seety außerhalb der eigenen Leiste zeichnet - bei Problemen nach einem Spiel-Update einfach ausschalten.",
    "Muestra la variación junto a la población y el dinero en la barra inferior del juego, que de otro modo solo la enseña al pasar el cursor. La cifra es del propio juego, no calculada por Seety. Es lo único que Seety dibuja fuera de su barra: desactívalo si una actualización del juego lo estropea.",
    "Affiche la variation à côté de la population et de l'argent sur la barre du bas du jeu, qui ne la montre sinon qu'au survol. Le chiffre vient du jeu, pas d'un calcul de Seety. C'est la seule chose que Seety dessine hors de sa propre barre : désactivez-la si une mise à jour du jeu la met en défaut.",
    "Mostra la variazione accanto a popolazione e denaro nella barra in basso del gioco, che altrimenti la mostra solo al passaggio del cursore. Il numero è quello del gioco, non calcolato da Seety. È l'unica cosa che Seety disegna fuori dalla propria barra: disattivala se un aggiornamento del gioco la rende sbagliata.",
    "ゲーム下部バーの人口と資金の横に増減を表示します。通常はカーソルを合わせたときしか表示されません。数値はゲーム自身のもので、Seety が計算したものではありません。Seety が自分のバーの外に描く唯一の要素なので、ゲームの更新で表示が崩れたらオフにしてください。",
    "게임 하단 바의 인구와 자금 옆에 증감을 표시합니다. 평소에는 마우스를 올려야만 보입니다. 이 수치는 게임이 제공하는 값이며 Seety가 계산한 것이 아닙니다. Seety가 자체 바 밖에 그리는 유일한 요소이므로, 게임 업데이트 후 이상하면 끄세요.",
    "Pokazuje zmianę obok liczby mieszkańców i pieniędzy na dolnym pasku gry, który w przeciwnym razie wyświetla ją tylko po najechaniu kursorem. Liczba pochodzi z gry, nie z obliczeń Seety. To jedyna rzecz, którą Seety rysuje poza własnym paskiem - wyłącz, jeśli aktualizacja gry ją zepsuje.",
    "Mostra a variação ao lado da população e do dinheiro na barra inferior do jogo, que de outro modo só a exibe ao passar o cursor. O número é do próprio jogo, não calculado pelo Seety. É a única coisa que o Seety desenha fora da própria barra: desative se uma atualização do jogo a deixar errada.",
    "Показывает изменение рядом с населением и деньгами на нижней панели игры, которая иначе выводит его только при наведении. Число берётся у самой игры, а не рассчитывается Seety. Это единственное, что Seety рисует за пределами своей панели, - отключите, если после обновления игры оно отображается неверно.",
    "在游戏底部栏的人口和资金旁显示变化，否则只有悬停时才看得到。该数字来自游戏本身，不是 Seety 计算的。这是 Seety 唯一画在自己栏之外的内容，若游戏更新后显示异常请关闭。",
    "在遊戲底部列的人口與資金旁顯示變化，否則只有停留游標時才看得到。該數字來自遊戲本身，不是 Seety 計算的。這是 Seety 唯一畫在自己列之外的內容，若遊戲更新後顯示異常請關閉。")
add("option.label.IconOutline",
    "White outline on icons", "Weißer Rand um Symbole", "Contorno blanco en los iconos",
    "Contour blanc sur les icônes", "Contorno bianco sulle icone", "アイコンの白フチ",
    "아이콘 흰색 테두리", "Biały kontur ikon", "Contorno branco nos ícones",
    "Белый контур значков", "图标白色描边", "圖示白色描邊")
add("option.desc.IconOutline",
    "Draws a white edge around each icon on the bar, so a dark icon stays legible over a dark building. Turn it off for a flatter look; the readings do not change either way.",
    "Zeichnet einen weißen Rand um jedes Symbol der Leiste, damit ein dunkles Symbol auch über einem dunklen Gebäude lesbar bleibt. Ausschalten für ein flacheres Bild; an den Werten ändert sich nichts.",
    "Dibuja un borde blanco alrededor de cada icono de la barra, para que un icono oscuro siga siendo legible sobre un edificio oscuro. Desactívalo para un aspecto más plano; las lecturas no cambian.",
    "Trace un contour blanc autour de chaque icône de la barre, pour qu'une icône sombre reste lisible sur un bâtiment sombre. Désactivez-le pour un rendu plus plat ; les valeurs ne changent pas.",
    "Disegna un bordo bianco attorno a ogni icona della barra, così un'icona scura resta leggibile sopra un edificio scuro. Disattivalo per un aspetto più piatto; le letture non cambiano.",
    "バーの各アイコンに白いフチを描き、暗い建物の上でも暗いアイコンが読み取れるようにします。オフにするとフラットな見た目になります。数値は変わりません。",
    "바의 각 아이콘에 흰색 테두리를 그려 어두운 건물 위에서도 어두운 아이콘이 잘 보이게 합니다. 끄면 더 평평한 모습이 됩니다. 수치는 바뀌지 않습니다.",
    "Rysuje biały kontur wokół każdej ikony na pasku, dzięki czemu ciemna ikona pozostaje czytelna na tle ciemnego budynku. Wyłącz, aby uzyskać płaszczy wygląd; odczyty się nie zmieniają.",
    "Desenha uma borda branca em volta de cada ícone da barra, para que um ícone escuro continue legível sobre um edifício escuro. Desative para um visual mais plano; as leituras não mudam.",
    "Рисует белый контур вокруг каждого значка на панели, чтобы тёмный значок оставался различим на фоне тёмного здания. Выключите для более плоского вида; показатели не меняются.",
    "在状态条的每个图标周围绘制白色描边，让深色图标在深色建筑上依然清晰。关闭可获得更扁平的外观；读数不会改变。",
    "在狀態條的每個圖示周圍繪製白色描邊，讓深色圖示在深色建築上依然清晰。關閉可獲得更扁平的外觀；讀數不會改變。")

add("option.label.ZoneTransparency",
    "Fade the zoning grid", "Bauraster abschwächen", "Atenuar la cuadrícula de zonificación",
    "Atténuer la grille de zonage", "Attenua la griglia di zonizzazione", "区画グリッドを薄く",
    "구획 격자 흐리게", "Przygaś siatkę stref", "Esmaecer a grade de zoneamento",
    "Приглушить сетку зон", "淡化分区网格", "淡化分區網格")
add("option.desc.ZoneTransparency",
    "Halves the opacity of the zoning cells drawn along the roads, so the ground shows through while you build. Off means the game looks exactly as it shipped. Disabled while Zone Color Changer is installed: both write the same colours, and the last one to write would silently win.",
    "Halbiert die Deckkraft der Bauzellen entlang der Straßen, sodass der Untergrund beim Bauen durchscheint. Aus bedeutet, dass das Spiel genau wie ausgeliefert aussieht. Deaktiviert, solange Zone Color Changer installiert ist: beide schreiben dieselben Farben, und der letzte Schreibvorgang würde unbemerkt gewinnen.",
    "Reduce a la mitad la opacidad de las celdas de zonificación junto a las carreteras, para que el terreno se vea al construir. Desactivado, el juego se ve tal y como se publicó. Se desactiva si Zone Color Changer está instalado: ambos escriben los mismos colores y el último en escribir ganaría sin avisar.",
    "Réduit de moitié l'opacité des cases de zonage le long des routes, pour voir le sol pendant la construction. Désactivé, le jeu a exactement son apparence d'origine. Désactivé tant que Zone Color Changer est installé : les deux écrivent les mêmes couleurs, et le dernier à écrire l'emporterait sans prévenir.",
    "Dimezza l'opacità delle celle di zonizzazione lungo le strade, così il terreno si vede mentre costruisci. Da spento il gioco appare esattamente come è stato pubblicato. Disattivata se è installato Zone Color Changer: entrambi scrivono gli stessi colori e l'ultimo a scrivere vincerebbe senza dirlo.",
    "道路沿いの区画セルの不透明度を半分にし、建設中も地面が透けて見えるようにします。オフなら発売時のままの見た目です。Zone Color Changer が導入されている間は無効です。どちらも同じ色を書き換えるため、後から書いた方が黙って勝ってしまいます。",
    "도로변 구획 칸의 불투명도를 절반으로 낮춰 건설 중에도 지면이 비쳐 보이게 합니다. 끄면 출시 당시 그대로의 모습입니다. Zone Color Changer가 설치된 동안에는 비활성화됩니다. 둘 다 같은 색을 쓰기 때문에 나중에 쓴 쪽이 조용히 이깁니다.",
    "Zmniejsza o połowę krycie komórek stref przy drogach, dzięki czemu podczas budowy widać teren. Wyłączone oznacza wygląd dokładnie taki, jak w wydanej grze. Nieaktywne, gdy zainstalowany jest Zone Color Changer: oba zapisują te same kolory, a ten, który zapisze później, wygrałby po cichu.",
    "Reduz à metade a opacidade das células de zoneamento ao longo das vias, para que o terreno apareça enquanto você constrói. Desligado, o jogo fica exatamente como foi lançado. Desativado enquanto o Zone Color Changer estiver instalado: ambos escrevem as mesmas cores, e o último a escrever venceria silenciosamente.",
    "Уменьшает непрозрачность ячеек зонирования вдоль дорог вдвое, чтобы во время строительства была видна земля. Выключено - игра выглядит ровно так, как вышла. Недоступно, пока установлен Zone Color Changer: оба записывают одни и те же цвета, и последний записавший победил бы незаметно.",
    "将道路两侧分区格子的不透明度减半，建造时可以看见地面。关闭时游戏外观与发行时完全一致。安装了 Zone Color Changer 时此项停用：两者写入同一组颜色，后写入的一方会悄悄覆盖另一方。",
    "將道路兩側分區格子的不透明度減半，建造時可以看見地面。關閉時遊戲外觀與發行時完全一致。安裝了 Zone Color Changer 時此項停用：兩者寫入同一組顏色，後寫入的一方會悄悄覆蓋另一方。")

io.open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "_stage1_ok"), "w").write("ok")
print("stage 1:", len(D), "chiavi")
