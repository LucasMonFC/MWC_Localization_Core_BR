# MWC Localization Core

Sistema plugin para MSCLoader do My Winter Car que permite tradução automática sem modificar o código.

Veja em [NexusMods](https://www.nexusmods.com/mywintercar/mods/197)

## Início Rápido

### Para criadores de pacotes de idioma

1. **Copie os arquivos de modelo** de `dist/`
2. **Edite dist\Assets\MWC_Localization_Core_BR/config.txt`** com as configurações do seu idioma
3. **Atualize os arquivos de tradução**:
   - `translate.txt` - Texto principal da interface
   - `translate_msc.txt (opcional)` - textos do My Summer Car (compatibilidade)
   - `translate_magazine.txt` - Conteúdo da revista Classificados
   - `translate_teletext.txt` - Conteúdo da TV/Teletexto
   - `translate_mod.txt (opcional)` - Conteúdo de mods
4. **(Opcional)** Crie fontes customizadas em `fonts.unity3d`
5. **Teste no jogo com recarga (F8)!**

### Para desenvolvedores

```
Delete: <TargetFrameworkProfile>v3.5</TargetFrameworkProfile> no MWC_Localization_Core.csproj apos rode esse comando no PowerShell
'& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" ".\MWC_Localization_Core.csproj" /p:Configuration=Release'
```

## Funcionalidades

**Tradução automática** - Escaneia componentes TextMesh e substitui o texto  
**Tradução de teletexto/TV** - Notícias, tempo e receitas traduzidas  
**Tradução de revista** - Tratamento especial para a revista Classificados  
**Fontes configuráveis** - Mapeie fontes do jogo para fontes localizadas  
**Ajustes de posição** - Ajuste fino do posicionamento do texto por idioma  
**Recarga ao vivo** - Pressione F8 para testar alterações sem reiniciar  
**Suporte a alfabetos não latinos** - coreano, japonês, chinês, cirílico etc.  
**Compatibilidade com My Summer Car** - Use tradução do MSC como base

## Estrutura de arquivos

```
My Winter Car\Mods
├── My Winter Car\Mods\Assets\MWC_Localization_Core_BR
│   ├── config.txt                  # Configuração do idioma
│   ├── translate.txt               # Traduções principais da interface
│   ├── translate_magazine.txt      # Traduções da revista Classificados
│   ├── translate_teletext.txt      # Traduções de TV/Teletexto
│   ├── translate_msc.txt           # Opcional: compatibilidade My Summer Car
│   ├── translate_mod.txt           # Opcional: traduções de mods
│   └── fonts.unity3d               # Opcional: bundle de fontes customizadas
└── MWC_Localization_Core.dll       # Módulo principal do plugin
```

## Configuração (config.txt)

### Configurações básicas

```ini
# Informações do seu idioma
LANGUAGE_NAME = Coreano
LANGUAGE_CODE = ko-KR
```

| Configuração | Propósito | Exemplo |
|-------------|-----------|---------|
| `LANGUAGE_NAME` | Nome exibido | `Coreano`, `Español`, `日本語` |
| `LANGUAGE_CODE` | Código ISO do idioma | `ko-KR`, `es-ES`, `ja-JP` |

### Mapeamento de fontes

Se estiver usando fontes personalizadas, mapeie as fontes originais do jogo para elas no `config.txt`:

```ini
[FONTS]
OriginalGameFont = SuaFontePersonalizada
FugazOne-Regular = MinhaFonte-Bold
Heebo-Black = MinhaFonte-Regular
```

Os assets de fontes devem existir em `fonts.unity3d` com os nomes correspondentes (valores à direita).

## Arquivos de tradução

### translate.txt - Traduções da interface principal

Arquivo principal de tradução que cobre as linhas do My Winter Car.

```
# Comentários usam #
# As chaves são normalizadas automaticamente: MAIÚSCULAS, sem espaços

BEER = Cerveja
BUCKET = Balde
MONDAY = Segunda-feira
WITHDRAWAL = Retirada

# Suporte a múltiplas linhas (Use \n)
Welcome to My Winter Car = Bem-vindo ao My Winter Car\nSeja bem-vindo
```

### translate_magazine.txt - Revista Classificados

Este arquivo contém lógica especial para lidar com palavras aleatórias separadas por vírgula e a linha de preço na revista Classificados.

```
# Abreviações da revista (separadas por vírgula)
headlgh.l = esq.farol
headgskt. = cab.gaxeta
supp.arm = susp.braco

# Rótulo de telefone para linhas de preço
# Usado em linhas como "h.149,- puh.123456" -> "149 MK, ${PHONE} - (08)123456"
PHONE = Telefone
```

**Formatação específica da revista:**
- Palavras abreviadas usam pontos e vírgulas
- Linhas de preço recebem tratamento especial para números de telefone
- Diferente do texto normal da interface

### translate_teletext.txt - Conteúdo TV/Teletexto

Traduções por categoria para o teletexto da TV (notícias, tempo, receitas etc.) e páginas de chat da TV.
Este arquivo separado foi introduzido para evitar que o jogo sobrescreva constantemente as traduções do plugin.

**A ORDEM & [CATEGORIA] IMPORTAM!**
Pelo menos neste arquivo. Recomenda-se NÃO modificar a ordem/categories.

```
# As seções de categoria correspondem às páginas do teletexto
[day]
MONDAY = Segunda-feira
TUESDAY = Terça-feira

[kotimaa]
# Manchetes de notícias domésticas (na ordem em que aparecem)
MAKELIN CEO FIRED = MAKELIN CEO DEMITIDO
TAXI REFORM PLANNED = REFORMA DO TÁXI PLANEJADA

[urheilu]
# Notícias esportivas
FOOTBALL RESULTS = RESULTADOS DE FUTEBOL

# Formato multilinha:
Long news
Headline here
=
Notícia longa
Manchete
```

**Categorias:**
- `day` - Nomes dos dias
- `kotimaa` - Notícias domésticas
- `ulkomaat` - Notícias estrangeiras  
- `talous` - Notícias econômicas
- `urheilu` - Notícias esportivas
- `ruoka` - Receitas
- `ajatus` - Citações
- `kulttuuri` - Cultura

Observe que algumas linhas da TV podem não 'parecer traduzidas' devido ao motivo mencionado acima.

### translate_msc.txt - Compatibilidade com My Summer Car (Opcional)

Você pode reutilizar arquivos de tradução do My Summer Car como base. Muitos textos da interface são compartilhados entre os jogos.

O conteúdo de `translate.txt` (específico do MWC) irá sobrescrever as entradas de `translate_msc.txt`.

## Ajustes de texto (Opcional)

Ajuste fino do posicionamento, tamanho, espaçamento e largura do texto para melhor aparência sem alterar o código.

### Configuração

```ini
[POSITION_ADJUSTMENTS]
Conditions = X,Y,Z[,Tamanho da fonte,Espaçamento entre linhas,Escala de largura]
```

### Sintaxe de condições

| Condição | Quando corresponde |
|---------|-------------------|
| `Contains(path)` | O caminho contém o texto |
| `EndsWith(path)` | O caminho termina com o texto |
| `StartsWith(path)` | O caminho começa com o texto |
| `Equals(path)` | O caminho corresponde exatamente |
| `!Contains(path)` | O caminho NÃO contém (negação) |

**Dica:** Use o console BepInEx (F12) para ver os caminhos dos GameObjects quando o texto aparece. Isso ajuda a escrever as condições. (Não esta funcionando no MSCLoader)

### Exemplos

```ini
# Apenas ajuste de posição: mover labels do HUD para baixo (Y = -0.05)
Contains(GUI/HUD/) & EndsWith(/HUDLabel) = 0,-0.05,0

# Tornar o texto mais largo: escala de largura 1.2x (último parâmetro)
Contains(Systems/Narrow/Text) = 0,0,0,,,1.2

# Ajuste completo: posição + tamanho + espaçamento de linha + largura
Contains(GUI/Menu/Title) = 0,0.1,0,0.12,1.0,1.3

# Pular parâmetros com vírgulas: posição + escala de largura (pular tamanho da fonte e espaçamento)
Contains(PERAPORTTI/ATMs/) & EndsWith(/Text) = 0,0.25,0,,,0.9

# Combinar múltiplas condições com negação
Contains(PERAPORTTI/ATMs/) & !Contains(/Row) & EndsWith(/Text) = 0,0.25,0
```

### Formato dos parâmetros

```
X,Y,Z[,FontSize,LineSpacing,WidthScale]
```

| Parâmetro | Tipo | Propósito | Exemplos |
|-----------|------|----------|---------|
| **X** | Obrigatório | Deslocamento horizontal (+ para direita, - para esquerda) | `0`, `0.5`, `-0.3` |
| **Y** | Obrigatório | Deslocamento vertical (+ para cima, - para baixo) | `0`, `0.25`, `-0.05` |
| **Z** | Obrigatório | Deslocamento em profundidade (raramente necessário) | `0` |
| **FontSize** | Opcional | Tamanho do caractere (TextMesh.characterSize) | `0.1`, `0.15`, `0.2` |
| **LineSpacing** | Opcional | Multiplicador de espaçamento entre linhas | `1.0`, `1.2`, `0.8` |
| **WidthScale** | Opcional | Escala de largura dos caracteres (transform.localScale.x) | `1.0`, `1.2` (mais largo), `0.8` (mais estreito) |

**Dicas:**
- Deixe parâmetros opcionais vazios para pular: `0,0,0,,1.2` (pular FontSize, definir LineSpacing)
- Use `WidthScale > 1.0` para deixar o texto mais largo (bom para fontes estreitas)
- Use `WidthScale < 1.0` para deixar o texto mais estreito (bom para layouts condensados)
- Combine com FontSize para controlar altura e largura independentemente

## Criando fontes customizadas (Opcional)

Para idiomas que precisam de suporte especial de fontes (melhor legibilidade, caracteres especiais etc.):

1. **Prepare as fontes** - TrueType (.ttf) ou OpenType (.otf)
2. **Crie assets no Unity** - Use Unity 5.0.0f4 (mesma versão do My Winter Car)
3. **Construir AssetBundle** - Nomeie como `fonts.unity3d`
4. **Caso os nomes** - Os nomes dos assets de fonte devem corresponder aos valores na seção [FONTS] do `config.txt`
5. **Coloque em l10n_assets** - Coloque `fonts.unity3d` junto com os outros arquivos de tradução

**Observações sobre o Unity:**
- O Unity 5.0.0f4 tem problemas de licenciamento - instale 5.6.7f1 primeiro para ativar, depois execute 5.0.0f4
- O target do AssetBundle deve corresponder ao jogo (geralmente Windows Standalone)

## Testes e desenvolvimento

### Recarga ao vivo (tecla F8)

Pressione **F8** no jogo para recarregar instantaneamente todos os arquivos de configuração e tradução:
- Edite `config.txt`, arquivos de tradução, etc.
- Não é necessário reiniciar o jogo
- Perfeito para trabalho iterativo de tradução

### Fluxo de depuração

1. Ative o console BepInEx: Edite `BepInEx/config/BepInEx.cfg`
   - Defina `Enabled = true` em `[Logging.Console]`
2. Inicie o jogo e pressione **F12** para abrir o console
3. Verifique erros de configuração e o status das traduções
4. Observe os caminhos dos GameObjects quando o texto aparece (ajuda nos ajustes de posição)
5. Edite os arquivos e pressione **F8** para testar as alterações
6. Repita até ficar perfeito

### Problemas comuns

**Texto não está sendo traduzido?**
- Ative mensagens no console via configurações do MSCLoader Mod
- Verifique o console (F12) para erros
- Certifique-se de que a chave corresponde
- Para teletexto, verifique se você está usando a seção de categoria correta

**Fonte errada?**
- Verifique os nomes das fontes na seção [FONTS] do `config.txt`
- Verifique se `fonts.unity3d` existe e carrega corretamente
- O console exibirá mensagens "Loaded [font] for [original]"

**Posição do texto deslocada?**
- Use o `Developer Toolkit` ou outra ferramenta para encontrar o caminho do GameObject
- Adicione ajuste de posição no `config.txt`
- Teste com recarga (F8)

### Compilando o plugin

```bash
Delete: <TargetFrameworkProfile>v3.5</TargetFrameworkProfile> no MWC_Localization_Core.csproj apos rode esse comando no PowerShell
'& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" ".\MWC_Localization_Core.csproj" /p:Configuration=Release'
```