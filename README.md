# SyllabusAI

SyllabusAI is an AI-supported web-based syllabus management platform that transforms conventional syllabus documents into an interactive educational environment. Developed as an interdisciplinary Capstone Project, this platform bridges the gap between static academic documents and dynamic student engagement.

## Overview

Traditional course syllabuses are often static PDFs, limiting engagement and increasing administrative workload. SyllabusAI solves this by providing an intelligent platform where instructors can easily manage course details, and students can interact with an AI assistant that answers questions based strictly on the official syllabus. 

The core of the system is built upon a Retrieval-Augmented Generation (RAG) architecture that parses syllabus content and generates responses grounded exclusively in verified text, eliminating AI hallucinations and providing direct citations.

## Key Features

### For Students
* **Interactive Dashboard:** Access structured syllabus information through highly scannable, pastel-colored grid cards.
* **Grounded AI Chatbot:** Ask questions about grading, attendance, or schedules and receive real-time, hallucination-free answers verified by direct source citation links.
* **Smart Lock Progression:** A behavioral reading progress bar that unlocks the AI assistant only after the student has scrolled through and reviewed the syllabus document.
* **Continuous Feedback:** Submit structured weekly evaluations and end-of-semester surveys to help improve course pacing.

### For Instructors
* **Automated Ingestion Engine:** Upload raw syllabus files (PDF/DOCX) and trigger the system to automatically parse and populate a 14-week chronological schedule table.
* **AI Teaching Assistant:** Generate localized active learning classroom scripts based on weekly topics.
* **Feedback Analytics:** Publish surveys and review aggregated student analytics reports (via progress bars and star ratings) directly from the administrative workspace.

## System Architecture & Tech Stack

The application follows a modern Domain-Driven Design (DDD) approach within an N-Tier architecture.

* **Frontend (Presentation Layer):** React.js, HTML, CSS, JavaScript.
* **Backend (API Layer):** ASP.NET Core 8 Web API, C#.
* **Database (Data Layer):** Microsoft SQL Server managed via Entity Framework Core 8.
* **AI Integration:** OpenAI API (gpt-4o for chat, text-embedding-3-small for vector embeddings).
* **Document Processing:** PdfPig 0.1.8 for text extraction and section detection.
* **Security:** JWT (JSON Web Tokens) and Role-Based Access Control (RBAC) with BCrypt password hashing.

## Engineering Contributions

As part of the Software Engineering team for this project, my core contributions included:

* **Architected** the backend infrastructure, relational database schemas, and RESTful API endpoints using .NET 8 and SQL Server.
* **Implemented** the Retrieval-Augmented Generation (RAG) methodology, designing the logic that parses raw syllabus PDFs into categorized text chunks and processes them into vector embeddings.
* **Integrated** the OpenAI language models to ensure safe, scope-gated responses, and secured user sessions via JWT and RBAC.
* **Optimized** database queries and API loops to maintain high-throughput performance, successfully achieving an ultra-low page rendering speed of 320 ms.

## Performance & Validation

* Achieved an absolute 100% Task Completion Rate (TCR) during rigorous User Acceptance Testing (UAT).
* Validated system layout and efficiency against Nielsen's heuristics, achieving an "Elite Usability Tier" (Grade A) for the student focus group on the System Usability Scale (SUS).
* Reduced chatbot inference latency to an average of 8 seconds while maintaining strict academic integrity.
