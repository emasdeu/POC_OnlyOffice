{{/*
Expand the name of the chart.
*/}}
{{- define "onlyoffice-documentserver.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "onlyoffice-documentserver.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "onlyoffice-documentserver.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "onlyoffice-documentserver.labels" -}}
helm.sh/chart: {{ include "onlyoffice-documentserver.chart" . }}
{{ include "onlyoffice-documentserver.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "onlyoffice-documentserver.selectorLabels" -}}
app.kubernetes.io/name: {{ include "onlyoffice-documentserver.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "onlyoffice-documentserver.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "onlyoffice-documentserver.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Database host
*/}}
{{- define "onlyoffice-documentserver.database.host" -}}
{{- if .Values.postgresql.enabled }}
{{- printf "%s-postgresql" .Release.Name }}
{{- else }}
{{- .Values.postgresql.externalDatabase.host }}
{{- end }}
{{- end }}

{{/*
Database port
*/}}
{{- define "onlyoffice-documentserver.database.port" -}}
{{- if .Values.postgresql.enabled }}
5432
{{- else }}
{{- .Values.postgresql.externalDatabase.port }}
{{- end }}
{{- end }}

{{/*
RabbitMQ host
*/}}
{{- define "onlyoffice-documentserver.rabbitmq.host" -}}
{{- if .Values.rabbitmq.enabled }}
{{- printf "%s-rabbitmq" .Release.Name }}
{{- else }}
{{- .Values.rabbitmq.externalRabbitMQ.host }}
{{- end }}
{{- end }}

{{/*
RabbitMQ port
*/}}
{{- define "onlyoffice-documentserver.rabbitmq.port" -}}
{{- if .Values.rabbitmq.enabled }}
5672
{{- else }}
{{- .Values.rabbitmq.externalRabbitMQ.port }}
{{- end }}
{{- end }}

{{/*
RabbitMQ username
*/}}
{{- define "onlyoffice-documentserver.rabbitmq.username" -}}
{{- if .Values.rabbitmq.enabled }}
{{- .Values.rabbitmq.auth.username }}
{{- else }}
{{- .Values.rabbitmq.externalRabbitMQ.username }}
{{- end }}
{{- end }}

{{/*
RabbitMQ password
*/}}
{{- define "onlyoffice-documentserver.rabbitmq.password" -}}
{{- if .Values.rabbitmq.enabled }}
{{- .Values.rabbitmq.auth.password }}
{{- else }}
{{- .Values.rabbitmq.externalRabbitMQ.password }}
{{- end }}
{{- end }}
