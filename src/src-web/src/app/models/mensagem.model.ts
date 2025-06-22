export interface MensagemDto {
  texto: string;
  nome?: string;
  telefone?: string;
  modeloIA: string;
}

export interface ChatMensagemDto {
  texto: string;
  origem: string; // 'cliente' ou 'bot'
  dataCriacao: Date;
}

export interface RespostaDto {
  sucesso: boolean;
  mensagem: string;
  erro?: string;
  clienteIdentificado?: boolean;
  nome?: string;
  telefone?: string;
  contaExcluida?: boolean;
  dadosTemporarios?: any;
  historico?: ChatMensagemDto[];
}

export interface ModeloIADto {
  id: string;
  nome: string;
  tipo: string; // 'chatgpt', 'claude', 'local'
}

export interface ModelosDisponiveis {
  modelos: ModeloIADto[];
} 