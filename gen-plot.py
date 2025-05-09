import pandas as pd
import matplotlib.pyplot as plt
import os

plt.style.use('seaborn-v0_8-whitegrid')
plt.rcParams.update({
    'font.size': 12,
    'lines.markersize': 8,
    'lines.linewidth': 1.5
})

# Чтение данных
df = pd.read_csv('data.txt', sep='\s+', header=None, decimal=',',
                 names=['lambda', 'mu', 'P0_theory', 'Pn_theory', 'Q_theory', 
                        'A_theory', 'k_theory', 'P0_exp', 'Pn_exp',
                        'Q_exp', 'A_exp', 'k_exp'])
df = df.sort_values('lambda') 

# Создаем папку для результатов, если ее нет
os.makedirs('result', exist_ok=True)

# Построение графиков
metrics = [
    ('P0', 'Вероятность простоя'),
    ('Pn', 'Вероятность отказа'),
    ('Q', 'Относительная пропускная способность'),
    ('A', 'Абсолютная пропускная способность'),
    ('k', 'Среднее число занятых каналов')
]

i = 1
for metric, title in metrics:
    fig, ax = plt.subplots(figsize=(10, 6))

    # Теоретические кривые (сплошная линия с точками)
    ax.plot(
        df['lambda'], 
        df[f'{metric}_theory'], 
        color='navy',
        linestyle='-',  # Сплошная линия
        marker='o',     # Кружки в точках данных
        markersize=6,
        markerfacecolor='navy',  # Заливка маркеров
        markeredgewidth=1.5,
        label='Теоретические значения'
    )

    # Экспериментальные данные (красная линия с кружками)
    ax.plot(
        df['lambda'], 
        df[f'{metric}_exp'], 
        color='crimson',
        linestyle='-',  # Сплошная линия
        marker='o',     # Кружки в точках данных
        markersize=6,
        markerfacecolor='white',  # Белая заливка маркеров
        markeredgecolor='crimson',  # Граница маркеров
        markeredgewidth=1.5,
        label='Экспериментальные значения'
    )

    ax.set_xlabel('Интенсивность входного потока (λ)')
    ax.set_ylabel(title)
    # ax.set_title(f"{title} при μ = {df['mu'].iloc[0]}", pad=15)
    ax.set_title(f'Зависимость {title.lower()} от интенсивности потока\n(μ = {df["mu"].iloc[0]})')
    ax.legend()
    ax.grid(True, alpha=0.3)

    filename = os.path.join('result', f'p-{i}.png')
    plt.savefig(filename, dpi=300, bbox_inches='tight')
    plt.close()
    i += 1

print("Графики успешно построены и сохранены в папку 'result'")