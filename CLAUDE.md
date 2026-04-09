# AGENTS.md
## Codex GPT-5.4 Implementation Guide for File-Transformer

This document defines the architecture, constraints, and feature roadmap for AI agents working on this repository.

---

# 🧭 Core Principles

## 1. Preview-First Safety Model
- NEVER perform destructive filesystem actions without preview
- ALL operations must:
  - be planned first
  - be visible to the user
  - be reversible via rollback
- ALL paths must remain inside the selected root directory

## 2. Deterministic Core + AI Assistance
- Local logic is ALWAYS authoritative
- AI (Gemini) is advisory only
- AI suggestions must be validated before execution
- No direct AI-generated file operations

## 3. Reversibility is Mandatory
- Every filesystem mutation must be journaled
- Rollback must be reliable, testable, and idempotent

---

# 🏗 Architecture Overview

## Layers

### App (WPF / UI)
- `MainWindow.xaml`
- `MainWindowViewModel.cs`
- Wizard UI logic
- Localization

### Application
- `OrganizationWorkflowService`
- `PlanExecutionService`
- `RollbackService`
- Strategy recommendation layer (to be added)

### Domain
- Organization rules
- Strategy presets
- Naming policies
- Duplicate policies

### Infrastructure
- File system access
- Hashing
- Journaling
- Content extraction (PDF, DOCX, etc.)
- Gemini integration

---

# 🚀 Feature Requirements

---

## 🔁 1. Flawless Rollback System

### Requirements
- Support rollback of ANY historical run (not just latest)
- Rollback must be:
  - idempotent
  - safe against partial failures
  - transparent to user

### Implementation

#### Journal Enhancements
Each operation must store:
- Source path
- Destination path
- File hash
- File size
- Timestamp
- Operation type
- Pre-existing destination state
- Rollback status

#### Execution Model
- Write journal header BEFORE execution
- Append entries DURING execution
- Mark run complete AFTER execution

#### Rollback Features
- Select run from history
- Preview rollback plan
- Execute rollback safely
- Handle:
  - missing files
  - path conflicts
  - partial rollbacks

#### Tests (MANDATORY)
- Full rollback
- Partial rollback
- Conflict recovery
- Repeated rollback (idempotency)

---

## 🧹 2. Hash-Based Duplicate Removal

### Rules
- ONLY use file content hash (SHA-256)
- NEVER rely on filename

### Process
1. Group files by size
2. Hash only same-size files
3. Build duplicate groups

### Behavior
- Propose duplicates in preview
- NEVER auto-delete immediately

### Default Action
Move duplicates to: