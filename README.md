# RAG Demo - .NET 10 Minimal API

Demo di Retrieval-Augmented Generation con API minimale, embeddings e generazione tramite Ollama. I documenti e i vettori restano in memoria e vengono persi quando l'applicazione si riavvia.

## Requisiti

- .NET 10 SDK
- [Ollama](https://ollama.com/) in esecuzione su `http://localhost:11434`

Scarica i modelli usati dalla configurazione predefinita:

```powershell
ollama pull nomic-embed-text
ollama pull llama3.2
```

Avvia l'API dalla cartella del progetto:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --urls http://localhost:5000
```

Durante l'esecuzione, la console mostra l'avvio e la configurazione di Ollama, l'indicizzazione dei documenti, i segmenti recuperati per ogni domanda e la generazione della risposta. I log non stampano il contenuto integrale dei documenti o delle domande. La console evidenzia anche che i dati restano nell'indice in memoria e non vengono salvati su database.

La configurazione del provider si trova in `appsettings.json` e può essere sovrascritta con le variabili d'ambiente `Ollama__BaseUrl`, `Ollama__EmbeddingModel`, `Ollama__ChatModel` e `Ollama__TopK`.

## API

La documentazione interattiva è disponibile anche in produzione su
`http://localhost:5000/scalar`; il documento OpenAPI è disponibile su
`http://localhost:5000/openapi/v1.json`.

### Aggiungere un documento

```powershell
$document = @{
  title = "Manuale prodotto"
  content = "Il prodotto consente di gestire le richieste dei clienti..."
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/documents `
  -ContentType "application/json" -Body $document
```

Il testo viene suddiviso in segmenti sovrapposti; Ollama calcola un embedding per ciascun segmento e l'indice viene mantenuto in memoria.

### Caricare un file

L'endpoint multipart accetta file UTF-8 `.txt` e `.md` fino a 100.000 caratteri (400.000 byte). Il titolo del documento viene ricavato dal nome del file.

```powershell
curl.exe -F "file=@.\manuale.md" http://localhost:5000/api/documents/upload
```

Il file viene indicizzato come un documento aggiunto tramite `POST /api/documents`.

### Elencare o rimuovere documenti

```powershell
Invoke-RestMethod http://localhost:5000/api/documents
Invoke-RestMethod -Method Delete -Uri http://localhost:5000/api/documents/{id}
```

### Fare una domanda

```powershell
$question = @{ question = "Come si gestiscono le richieste dei clienti?" } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/chat `
  -ContentType "application/json" -Body $question
```

La risposta include i segmenti recuperati, il titolo del documento e il punteggio di similarità coseno. Se Ollama o i modelli non sono disponibili, l'API restituisce un errore esplicito.

## Endpoint

| Metodo | Percorso | Descrizione |
|---|---|---|
| `GET` | `/api/health` | Stato e configurazione corrente |
| `GET` | `/api/documents` | Elenco dei documenti indicizzati |
| `POST` | `/api/documents` | Indicizza un documento (`title`, `content`) |
| `POST` | `/api/documents/upload` | Carica e indicizza un file UTF-8 `.txt` o `.md` (`multipart/form-data`, campo `file`) |
| `DELETE` | `/api/documents/{id}` | Rimuove un documento e i suoi segmenti |
| `POST` | `/api/chat` | Risponde a una domanda (`question`) |
