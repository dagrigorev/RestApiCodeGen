# Swagger API Generator 🚀

Welcome to the Swagger API Generator project! This project allows you to upload a Swagger (OpenAPI) specification file, generate C# Web API code based on this specification, and dynamically serve the generated API. 🎉

## Features ✨

- Upload Swagger (OpenAPI) specification files 📂
- Generate server-side C# Web API code from the uploaded specification 🛠️
- Dynamically load and serve the generated API on-the-fly 🌐
- Simple logging to track the invocation of generated server methods 📋

## Prerequisites 📝

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or later
- A tool like Postman for testing the API endpoints (optional)

## Getting Started 🏁

Follow these steps to set up and run the project:

### 1. Clone the Repository 📥

```bash
git clone https://github.com/yourusername/SwaggerApiGenerator.git
cd SwaggerApiGenerator
```

### 2. Build the Project 🏗️
```bash
dotnet build
```

### 3. Run the Project ▶️
```bash
dotnet run
```
The application will start and be available at http://localhost:5000.

### Usage 📚
Upload a Swagger Specification File 📤
Use a tool like Postman to send a POST request to `http://localhost:5000/api/swaggerupload/upload`.
Attach your Swagger (OpenAPI) specification file in the form-data.
Example using curl:

```bash
curl -X POST "http://localhost:5000/api/swaggerupload/upload" -F "file=@path/to/your/swaggerfile.json"
```

### Serve the Generated API 🌐
Send a GET request to `http://localhost:5000/api/swaggerupload/serve`.
Check the console for logs indicating which controllers have been loaded.

Example using curl:
```bash
curl -X GET "http://localhost:5000/api/swaggerupload/serve"
```

### Project Structure 🏗️
```text
SwaggerApiGenerator/
├── Controllers/
│   └── SwaggerUploadController.cs
├── Program.cs
├── SwaggerApiGenerator.csproj
└── README.md
```

## Key Files and Directories 🗂️
`Program.cs`: Configures services, logging, and maps endpoints.
`Controllers/SwaggerUploadController.cs`: Contains endpoints for uploading Swagger files and serving generated APIs.

### Logging 📋
The project uses console logging to track the invocation of dynamically generated server methods. Check the console output to see logs indicating which controllers are being loaded.

### Contributing 🤝
Contributions are welcome! Feel free to open issues or submit pull requests.

### License 📄
This project is licensed under the MIT License. See the LICENSE file for details.