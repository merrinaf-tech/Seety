# -*- coding: utf-8 -*-
"""Everything the UI itself draws: headers, buttons, empty states, notes."""
from gen_locale import add

U = [
 # --- selected journey
 ("JOURNEY_SHOW", "Selected journey","Ausgewählte Fahrt","Viaje seleccionado","Trajet sélectionné","Viaggio selezionato","選択中の移動経路","선택한 이동 경로","Wybrana podróż","Viagem selecionada","Выбранная поездка","所选行程","所選行程"),
 ("JOURNEY_JAMS", "Show traffic jams","Staus anzeigen","Mostrar atascos","Afficher les embouteillages","Mostra ingorghi","渋滞を表示","교통 정체 표시","Pokaż korki","Mostrar congestionamentos","Показать пробки","显示拥堵","顯示壅塞"),
 ("JOURNEY_EMPTY", "Select a citizen or vehicle with an active journey.","Wähle einen Bürger oder ein Fahrzeug mit einer laufenden Fahrt.","Selecciona un ciudadano o vehículo que esté viajando.","Sélectionnez un citoyen ou un véhicule en déplacement.","Seleziona un cittadino o un veicolo con un viaggio in corso.","移動中の市民または車両を選択してください。","이동 중인 시민이나 차량을 선택하세요.","Wybierz mieszkańca lub pojazd będący w podróży.","Selecione um cidadão ou veículo em viagem.","Выберите жителя или транспортное средство в пути.","选择正在出行的市民或车辆。","選擇正在出行的市民或車輛。"),
 ("JOURNEY_HERE", "Now","Aktuell","Ahora","Actuellement","Adesso","現在地","현재 위치","Teraz","Agora","Сейчас","当前位置","目前位置"),
 ("JOURNEY_DESTINATION", "Destination","Ziel","Destino","Destination","Destinazione","目的地","목적지","Cel","Destino","Назначение","目的地","目的地"),
 ("JOURNEY_UNKNOWN", "Unavailable","Nicht verfügbar","No disponible","Indisponible","Non disponibile","不明","정보 없음","Brak danych","Indisponível","Нет данных","暂无信息","暫無資訊"),
 ("JOURNEY_REMAINING", "Remaining journey","Verbleibender Weg","Trayecto restante","Trajet restant","Percorso rimanente","残りの経路","남은 경로","Pozostała trasa","Trajeto restante","Оставшийся путь","剩余行程","剩餘行程"),
 ("JOURNEY_NO_PATH", "No remaining route is available.","Kein verbleibender Weg verfügbar.","No hay información del trayecto restante.","Aucun trajet restant disponible.","Nessun percorso rimanente disponibile.","残りの経路はありません。","남은 경로 정보가 없습니다.","Brak informacji o pozostałej trasie.","Nenhum trajeto restante disponível.","Нет данных об оставшемся пути.","暂无剩余路线信息。","暫無剩餘路線資訊。"),
 ("JOURNEY_OPEN_LINE", "Open transport line","Verkehrslinie öffnen","Abrir línea de transporte","Ouvrir la ligne de transport","Apri linea di trasporto","交通路線を開く","교통 노선 열기","Otwórz linię transportową","Abrir linha de transporte","Открыть транспортную линию","打开交通线路","開啟交通路線"),
 ("JOURNEY_GO", "Go to the destination","Zum Ziel springen","Ir al destino","Aller à la destination","Vai alla destinazione","目的地へ移動","목적지로 이동","Przejdź do celu","Ir para o destino","Перейти к месту назначения","前往目的地","前往目的地"),
 ("JOURNEY_TRUNCATED", "Only the first part of this journey is shown.","Nur der erste Teil dieses Weges wird angezeigt.","Solo se muestra la primera parte del trayecto.","Seule la première partie du trajet est affichée.","È mostrata solo la prima parte del percorso.","経路の最初の部分のみ表示しています。","경로의 첫 부분만 표시됩니다.","Wyświetlana jest tylko pierwsza część trasy.","Apenas a primeira parte do trajeto é exibida.","Показана только первая часть пути.","仅显示此行程的前半部分。","僅顯示此行程的前段。"),
 ("TRAFFIC_TRANSIT", "Public transport","Nahverkehr","Transporte público","Transports en commun","Trasporto pubblico","公共交通","대중교통","Transport publiczny","Transporte público","Общественный транспорт","公共交通","大眾運輸"),
 ("TRAFFIC_ROAD", "Road traffic","Straßenverkehr","Tráfico rodado","Trafic routier","Traffico stradale","道路交通","도로 교통","Ruch drogowy","Tráfego rodoviário","Дорожное движение","道路交通","道路交通"),
 # --- strip chrome
 ("CONFIG_BANNER", "Choose readings or move the bar","Anzeigen wählen oder die Leiste verschieben","Elige las lecturas o mueve la barra","Choisissez les indicateurs ou déplacez la barre","Scegli le letture o sposta la barra","表示する項目を選ぶか、バーを移動します","항목을 선택하거나 바를 옮기세요","Wybierz odczyty lub przesuń pasek","Escolha as leituras ou mova a barra","Выберите показатели или переместите панель","选择显示项或移动栏","選擇顯示項或移動列"),
 ("CONFIG_ON", "Done choosing","Auswahl beenden","Terminar","Terminer","Fine scelta","選択を終了","선택 완료","Zakończ wybór","Concluir","Готово","完成选择","完成選擇"),
 ("CONFIG_OFF", "Choose which readings to show: click the ones you want","Wähle die anzuzeigenden Werte: klicke die gewünschten an","Elige qué lecturas mostrar: haz clic en las que quieras","Choisissez les lectures à afficher : cliquez sur celles voulues","Scegli quali letture mostrare: clicca quelle che vuoi","表示する項目を選びます。必要なものをクリックしてください","표시할 항목을 고르세요. 원하는 항목을 클릭합니다","Wybierz odczyty do pokazania: kliknij te, których chcesz","Escolha quais leituras mostrar: clique nas que quiser","Выберите показатели: нажмите на нужные","选择要显示的读数：点击你想要的项","選擇要顯示的讀數：點擊你想要的項"),
 ("ICONS_HIDE", "Hide icons","Symbole ausblenden","Ocultar iconos","Masquer les icônes","Nascondi icone","アイコンを隠す","아이콘 숨기기","Ukryj ikony","Ocultar ícones","Скрыть значки","隐藏图标","隱藏圖示"),
 ("ICONS_SHOW", "Show icons","Symbole einblenden","Mostrar iconos","Afficher les icônes","Mostra icone","アイコンを表示","아이콘 표시","Pokaż ikony","Mostrar ícones","Показать значки","显示图标","顯示圖示"),
 ("ICONS_HIDE_TIP", "Hide the notification icons over the city","Benachrichtigungssymbole über der Stadt ausblenden","Ocultar los iconos de notificación sobre la ciudad","Masquer les icônes de notification au-dessus de la ville","Nascondi le icone di notifica sopra la città","街の上の通知アイコンを隠します","도시 위의 알림 아이콘을 숨깁니다","Ukryj ikony powiadomień nad miastem","Ocultar os ícones de notificação sobre a cidade","Скрыть значки уведомлений над городом","隐藏城市上方的通知图标","隱藏城市上方的通知圖示"),
 ("ICONS_SHOW_TIP", "Show the notification icons over the city","Benachrichtigungssymbole über der Stadt einblenden","Mostrar los iconos de notificación sobre la ciudad","Afficher les icônes de notification au-dessus de la ville","Mostra le icone di notifica sopra la città","街の上の通知アイコンを表示します","도시 위의 알림 아이콘을 표시합니다","Pokaż ikony powiadomień nad miastem","Mostrar os ícones de notificação sobre a cidade","Показать значки уведомлений над городом","显示城市上方的通知图标","顯示城市上方的通知圖示"),
 # --- empty states
 ("EMPTY_NOTHING", "Nothing to report","Nichts zu melden","Nada que informar","Rien à signaler","Nulla da segnalare","報告なし","보고할 내용 없음","Nic do zgłoszenia","Nada a relatar","Сообщать нечего","无内容","無內容"),
 ("EMPTY_FACTORS", "No factors reported","Keine Faktoren gemeldet","No se informan factores","Aucun facteur signalé","Nessun fattore segnalato","要因の報告なし","보고된 요인 없음","Brak zgłoszonych czynników","Nenhum fator relatado","Факторы не сообщаются","无相关因素","無相關因素"),
 ("EMPTY_TRADE", "Nothing traded yet","Noch kein Handel","Aún no hay comercio","Aucun échange pour l'instant","Ancora nessuno scambio","取引はまだありません","아직 거래 없음","Brak handlu","Ainda sem comércio","Торговли пока нет","尚无贸易","尚無貿易"),
 # --- tooltips
 ("TIP_FACTOR", "{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT} %","{FACTOR}: -{PERCENT} %","{FACTOR} : -{PERCENT} %","{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT}%","{FACTOR}：-{PERCENT}%","{FACTOR}：-{PERCENT}%"),
 ("TIP_GO_THERE", "click to go there","klicken, um hinzuspringen","haz clic para ir allí","cliquez pour y aller","clicca per andarci","クリックでその場所へ","클릭하면 그곳으로 이동","kliknij, aby tam przejść","clique para ir até lá","нажмите, чтобы перейти","点击前往","點擊前往"),
 ("TIP_OPEN_INFO", "click to open its info view","klicken, um die Infoansicht zu öffnen","haz clic para abrir su vista de información","cliquez pour ouvrir la vue d'information","clicca per aprire la vista informativa","クリックでインフォビューを開く","클릭하면 정보 보기를 엽니다","kliknij, aby otworzyć widok informacji","clique para abrir a visão de informação","нажмите, чтобы открыть режим информации","点击打开信息视图","點擊開啟資訊檢視"),
 ("TIP_SELECT_MODE", "click to select this mode in the transport overview","klicken, um diesen Verkehrsträger in der Übersicht zu wählen","haz clic para seleccionar este modo en el resumen de transporte","cliquez pour sélectionner ce mode dans l'aperçu des transports","clicca per selezionare questo mezzo nel riepilogo trasporti","クリックで交通概要のこの手段を選択","클릭하면 교통 개요에서 이 수단을 선택합니다","kliknij, aby wybrać ten środek w przeglądzie transportu","clique para selecionar este modo na visão geral de transporte","нажмите, чтобы выбрать этот вид в обзоре транспорта","点击在交通总览中选择该方式","點擊在交通總覽中選擇該方式"),
 # --- transport window
 ("SEC_PASSENGERS", "Passengers","Fahrgäste","Pasajeros","Passagers","Passeggeri","旅客","승객","Pasażerowie","Passageiros","Пассажиры","客运","客運"),
 ("SEC_CARGO", "Cargo","Fracht","Carga","Fret","Merci","貨物","화물","Ładunek","Carga","Грузы","货运","貨運"),
 # --- workforce table
 ("WF_EDUCATION", "Education","Bildung","Educación","Éducation","Istruzione","学歴","학력","Wykształcenie","Educação","Образование","教育","教育"),
 ("WF_TOTAL", "Total","Gesamt","Total","Total","Totale","合計","합계","Razem","Total","Всего","合计","合計"),
 ("WF_KIDS", "Kids","Kinder","Niños","Enfants","Bambini","子供","아동","Dzieci","Crianças","Дети","儿童","兒童"),
 ("WF_STUDENT", "Student","Schüler","Estudia","Élèves","Studenti","学生","학생","Uczniowie","Estudam","Учатся","在学","在學"),
 ("WF_OLD", "Old","Senioren","Mayores","Aînés","Anziani","高齢","고령","Seniorzy","Idosos","Пожилые","老年","老年"),
 ("WF_ADULTS", "Adults","Erwachsene","Adultos","Adultes","Adulti","成人","성인","Dorośli","Adultos","Взрослые","成人","成人"),
 ("WF_EMPLOYED", "Employed","Beschäftigt","Empleados","Employés","Occupati","就業","취업","Zatrudnieni","Empregados","Заняты","就业","就業"),
 ("WF_UNEMPLOYED", "Unemployed","Arbeitslos","Desempleados","Sans emploi","Disoccupati","失業","실업","Bezrobotni","Desempregados","Безработные","失业","失業"),
 ("WF_UNDER", "Under","Unterqualifiziert","Subempleo","Sous-qualifié","Sottoimpiego","過小就業","하향취업","Poniżej","Subemprego","Ниже уровня","低配","低配"),
 ("WF_OUT", "Out","Auswärts","Fuera","Dehors","Fuori","市外勤務","외부근무","Poza","Fora","Вне города","外出","外出"),
 ("WF_IN", "In","Einpendler","Entran","Entrants","Entranti","流入","유입","Dojazd","Entram","Въезд","流入","流入"),
 ("WF_JOBS", "Jobs","Stellen","Puestos","Postes","Posti","職","일자리","Posady","Postos","Места","岗位","職位"),
 ("WF_VACANT", "Vacant","Unbesetzt","Vacantes","Vacants","Liberi","空き","공석","Wolne","Vagos","Вакансии","空缺","空缺"),
 # --- demographics
 ("DEMO_AGE", "Age","Alter","Edad","Âge","Età","年齢","연령","Wiek","Idade","Возраст","年龄","年齡"),
 ("AGE_CHILDREN", "Children","Kinder","Niños","Enfants","Bambini","子供","아동","Dzieci","Crianças","Дети","儿童","兒童"),
 ("AGE_TEENS", "Teens","Jugendliche","Adolescentes","Adolescents","Adolescenti","10代","청소년","Nastolatki","Adolescentes","Подростки","青少年","青少年"),
 ("AGE_ADULTS", "Adults","Erwachsene","Adultos","Adultes","Adulti","成人","성인","Dorośli","Adultos","Взрослые","成人","成人"),
 ("AGE_SENIORS", "Seniors","Senioren","Mayores","Aînés","Anziani","高齢者","노년","Seniorzy","Idosos","Пожилые","老年人","老年人"),
 ("EDU_NONE", "None","Ohne","Ninguna","Aucune","Nessuna","なし","없음","Brak","Nenhuma","Нет","无","無"),
 ("EDU_POOR", "Poor","Gering","Baja","Faible","Bassa","低","낮음","Niskie","Baixa","Низкое","低","低"),
 ("EDU_EDUCATED", "Educated","Mittel","Media","Moyenne","Media","中","보통","Średnie","Média","Среднее","中","中"),
 ("EDU_WELL", "Well","Gut","Alta","Bonne","Buona","高","높음","Wyższe","Alta","Высокое","高","高"),
 ("EDU_HIGHLY", "Highly","Sehr gut","Muy alta","Très bonne","Ottima","最高","최고","Najwyższe","Muito alta","Высшее","最高","最高"),
 # --- resource tables
 ("RES_RESOURCE", "Resource","Ware","Recurso","Ressource","Risorsa","資源","자원","Towar","Recurso","Ресурс","资源","資源"),
 ("RES_WANTED", "Wanted","Gefragt","Demandado","Demandé","Richiesta","需要度","수요","Popyt","Procurado","Спрос","需求","需求"),
 ("RES_STAFF", "Staff","Personal","Personal","Personnel","Personale","人員","인력","Obsada","Pessoal","Персонал","人手","人手"),
 ("RES_SHOPS", "Shops","Geschäfte","Tiendas","Commerces","Negozi","店舗","상점","Sklepy","Lojas","Магазины","商店","商店"),
 ("RES_PLANTS", "Plants","Betriebe","Plantas","Usines","Impianti","工場","공장","Zakłady","Fábricas","Заводы","工厂","工廠"),
 ("RES_OFFICES", "Offices","Büros","Oficinas","Bureaux","Uffici","事業所","사무실","Biura","Escritórios","Офисы","办公","辦公"),
 ("RES_STOCK", "Stock","Bestand","Existencias","Stock","Scorte","在庫","재고","Zapas","Estoque","Запас","库存","庫存"),
 ("RES_MADE", "Made","Produziert","Producido","Produit","Prodotto","生産","생산","Produkcja","Produzido","Произведено","产量","產量"),
 # --- parking
 ("PARK_CARS", "Cars","Autos","Coches","Voitures","Auto","自動車","자동차","Samochody","Carros","Автомобили","汽车","汽車"),
 ("PARK_BIKES", "Bikes","Fahrräder","Bicicletas","Vélos","Bici","自転車","자전거","Rowery","Bicicletas","Велосипеды","自行车","自行車"),
 # --- demand rows
 ("ZONE_RES_LOW", "Residential low","Wohnen (niedrig)","Residencial baja","Résidentiel faible","Residenziale bassa","住宅（低密度）","주거 저밀도","Mieszkalna niska","Residencial baixa","Жилая (низкая)","低密度住宅","低密度住宅"),
 ("ZONE_RES_MED", "Residential medium","Wohnen (mittel)","Residencial media","Résidentiel moyen","Residenziale media","住宅（中密度）","주거 중밀도","Mieszkalna średnia","Residencial média","Жилая (средняя)","中密度住宅","中密度住宅"),
 ("ZONE_RES_HIGH", "Residential high","Wohnen (hoch)","Residencial alta","Résidentiel élevé","Residenziale alta","住宅（高密度）","주거 고밀도","Mieszkalna wysoka","Residencial alta","Жилая (высокая)","高密度住宅","高密度住宅"),
 ("ZONE_COMMERCIAL", "Commercial","Gewerbe","Comercial","Commercial","Commerciale","商業","상업","Handlowa","Comercial","Коммерческая","商业","商業"),
 ("ZONE_INDUSTRIAL", "Industrial","Industrie","Industrial","Industriel","Industriale","工業","공업","Przemysłowa","Industrial","Промышленная","工业","工業"),
 ("ZONE_OFFICE", "Office","Büro","Oficinas","Bureaux","Uffici","オフィス","사무","Biurowa","Escritórios","Офисная","办公","辦公"),
 ("PARKED_OF", "parked of","belegt von","ocupadas de","occupées sur","occupati su","／","／","zajęte z","ocupadas de","занято из","已占用／共","已佔用／共"),
]

for row in U:
    add("Seety." + row[0], *row[1:])

print("ui:", len(U))
