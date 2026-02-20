# Proberaum Transfer

Ein lokaler Datei-Transfer-Server für den schnellen Austausch von Dateien zwischen PC und Smartphone im selben WLAN (z. B. über Smartphone-Hotspot oder X-Air18).

---

## Funktion

Proberaum Transfer startet einen lokalen Webserver auf dem PC.  
Über die angezeigte URL oder den generierten QR-Code kann mit einem Smartphone, oder jedem beliebigen Gerät im Netzwerk, im Browser darauf zugegriffen werden.

Damit können Dateien:

- 📥 vom PC auf das Smartphone heruntergeladen werden  
- 📤 vom Smartphone auf den PC hochgeladen werden  

Es wird keine Cloud benötigt – der gesamte Datentransfer erfolgt lokal im gleichen Netzwerk.

---

## Desktop-Anwendung

![Main](https://github.com/user-attachments/assets/61f70c2f-6c21-4525-acdb-611ee35046da)

### Einstellungen

- **Ordner wählen**  
  Der gewählte Ordner dient als Stammverzeichnis für Upload und Download.

- **Port festlegen**  
  Standardwert: `5000`

- **Server starten / stoppen**

---

### Verbindungs-URL

- Anzeige der lokalen IP-Adresse des PCs (z. B. `http://192.168.x.x:5000`)
- URL kann kopiert oder direkt im Browser geöffnet werden
- QR-Code ermöglicht das schnelle Öffnen auf dem Smartphone

---

## Weboberfläche (Smartphone)

![Phone](https://github.com/user-attachments/assets/5f4b3a59-ba35-47f4-8334-a46875b4f1c6)

### Funktionen

- 📂 Ordner-Navigation
- 📄 Anzeige vorhandener Dateien
- 🔍 Live-Suche nach Dateinamen
- 📥 Download per Button
- 📤 Upload per Dateiauswahl oder Drag & Drop
- ⬆️ Navigation zur übergeordneten Ebene

Die Weboberfläche ist mobil optimiert und funktioniert in aktuellen Browsern.

---

## Voraussetzungen

- PC und Smartphone müssen sich im selben WLAN befinden  
  (z. B. Smartphone-Hotspot oder Router)

---

## Sicherheit

- Zugriff ausschließlich im lokalen Netzwerk
- Server läuft nur während die Anwendung aktiv ist
- Zugriff ist auf den gewählten Ordner beschränkt

---
